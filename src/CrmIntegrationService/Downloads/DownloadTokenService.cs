using System.Buffers.Text;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using CrmIntegrationService.Offers;
using Microsoft.Extensions.Options;

namespace CrmIntegrationService.Downloads;

public enum TokenStatus
{
    Valid,
    Invalid,
    Expired,
}

public sealed record DownloadTokenResult(TokenStatus Status, string? OfferSlug = null, Guid? LeadId = null);

public sealed record IssuedToken(string Token, DateTimeOffset ExpiresAt);

/// <summary>
/// Stateless download links: base64url("{slug}|{leadId}|{expiresUnix}") + "." + base64url(HMAC-SHA256).
/// The signature is checked before the contents are trusted, so a forged token never reports "expired".
/// </summary>
public sealed class DownloadTokenService(IOptions<DownloadOptions> options, TimeProvider time)
{
    private const int MaxTokenLength = 512;

    private readonly byte[] _key = Convert.FromBase64String(options.Value.SigningKey);

    public IssuedToken Issue(string offerSlug, Guid leadId)
    {
        var expiresAt = time.GetUtcNow().Add(options.Value.TokenLifetime);
        var payload = Encoding.UTF8.GetBytes(
            string.Create(CultureInfo.InvariantCulture, $"{offerSlug}|{leadId:N}|{expiresAt.ToUnixTimeSeconds()}"));

        var token = $"{Base64Url.EncodeToString(payload)}.{Base64Url.EncodeToString(HMACSHA256.HashData(_key, payload))}";
        return new IssuedToken(token, expiresAt);
    }

    public DownloadTokenResult Validate(string? token)
    {
        var invalid = new DownloadTokenResult(TokenStatus.Invalid);
        if (string.IsNullOrEmpty(token) || token.Length > MaxTokenLength)
        {
            return invalid;
        }

        var dot = token.IndexOf('.');
        if (dot <= 0 || dot == token.Length - 1 || dot != token.LastIndexOf('.'))
        {
            return invalid;
        }

        byte[] payload;
        byte[] signature;
        try
        {
            payload = Base64Url.DecodeFromChars(token.AsSpan(0, dot));
            signature = Base64Url.DecodeFromChars(token.AsSpan(dot + 1));
        }
        catch (FormatException)
        {
            return invalid;
        }

        if (!CryptographicOperations.FixedTimeEquals(signature, HMACSHA256.HashData(_key, payload)))
        {
            return invalid;
        }

        var parts = Encoding.UTF8.GetString(payload).Split('|');
        if (parts.Length != 3
            || !Slug.IsValid(parts[0])
            || !Guid.TryParseExact(parts[1], "N", out var leadId)
            || !long.TryParse(parts[2], NumberStyles.None, CultureInfo.InvariantCulture, out var expiresUnix))
        {
            return invalid;
        }

        return time.GetUtcNow().ToUnixTimeSeconds() >= expiresUnix
            ? new DownloadTokenResult(TokenStatus.Expired)
            : new DownloadTokenResult(TokenStatus.Valid, parts[0], leadId);
    }
}
