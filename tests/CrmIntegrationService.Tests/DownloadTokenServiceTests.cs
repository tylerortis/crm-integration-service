using System.Security.Cryptography;
using CrmIntegrationService.Downloads;
using Microsoft.Extensions.Options;

namespace CrmIntegrationService.Tests;

public class DownloadTokenServiceTests
{
    private static readonly DateTimeOffset Start = new(2026, 5, 4, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid LeadId = Guid.Parse("0f8fad5b-d9cb-469f-a165-70867728950e");

    private static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));

    private static DownloadTokenService Create(ManualTimeProvider time, string? key = null) =>
        new(Options.Create(new DownloadOptions { SigningKey = key ?? NewKey(), TokenLifetime = TimeSpan.FromMinutes(30) }), time);

    [Fact]
    public void Issued_token_validates_and_carries_offer_and_lead()
    {
        var service = Create(new ManualTimeProvider(Start));

        var issued = service.Issue("starter-checklist", LeadId);
        var result = service.Validate(issued.Token);

        Assert.Equal(new DownloadTokenResult(TokenStatus.Valid, "starter-checklist", LeadId), result);
        Assert.Equal(Start.AddMinutes(30), issued.ExpiresAt);
    }

    [Fact]
    public void Token_is_url_safe()
    {
        var token = Create(new ManualTimeProvider(Start)).Issue("starter-checklist", LeadId).Token;

        Assert.Matches("^[A-Za-z0-9_-]+\\.[A-Za-z0-9_-]+$", token);
    }

    [Fact]
    public void Expires_after_the_configured_lifetime()
    {
        var time = new ManualTimeProvider(Start);
        var service = Create(time);
        var token = service.Issue("starter-checklist", LeadId).Token;

        time.Advance(TimeSpan.FromMinutes(29));
        Assert.Equal(TokenStatus.Valid, service.Validate(token).Status);

        time.Advance(TimeSpan.FromMinutes(1));
        Assert.Equal(new DownloadTokenResult(TokenStatus.Expired), service.Validate(token));
    }

    [Fact]
    public void Tampered_payload_is_invalid()
    {
        var service = Create(new ManualTimeProvider(Start));
        var token = service.Issue("starter-checklist", LeadId).Token;
        var otherPayload = service.Issue("other-offer", LeadId).Token.Split('.')[0];

        var forged = otherPayload + "." + token.Split('.')[1];

        Assert.Equal(TokenStatus.Invalid, service.Validate(forged).Status);
    }

    [Fact]
    public void Tampered_signature_is_invalid()
    {
        var service = Create(new ManualTimeProvider(Start));
        var token = service.Issue("starter-checklist", LeadId).Token;

        // Change a character in the middle of the signature; the final base64 character
        // partly encodes padding bits, so changing it doesn't always change the bytes.
        var index = token.IndexOf('.') + 10;
        var tampered = token[..index] + (token[index] == 'A' ? 'B' : 'A') + token[(index + 1)..];

        Assert.Equal(TokenStatus.Invalid, service.Validate(tampered).Status);
    }

    [Fact]
    public void Token_signed_with_another_key_is_invalid()
    {
        var time = new ManualTimeProvider(Start);
        var token = Create(time).Issue("starter-checklist", LeadId).Token;

        Assert.Equal(TokenStatus.Invalid, Create(time).Validate(token).Status);
    }

    [Fact]
    public void Expired_but_forged_token_reports_invalid_not_expired()
    {
        var time = new ManualTimeProvider(Start);
        var token = Create(time).Issue("starter-checklist", LeadId).Token;
        time.Advance(TimeSpan.FromDays(1));

        Assert.Equal(TokenStatus.Invalid, Create(time).Validate(token).Status);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("no-dot")]
    [InlineData("a.b.c")]
    [InlineData(".")]
    [InlineData("!!!.???")]
    public void Malformed_tokens_are_invalid(string? token)
    {
        Assert.Equal(TokenStatus.Invalid, Create(new ManualTimeProvider(Start)).Validate(token).Status);
    }

    [Theory]
    [InlineData(32, true)]
    [InlineData(16, false)]
    public void Signing_key_must_be_at_least_32_bytes(int bytes, bool expected)
    {
        Assert.Equal(expected, DownloadOptions.HasValidKey(Convert.ToBase64String(new byte[bytes])));
        Assert.False(DownloadOptions.HasValidKey("not base64 at all"));
    }
}
