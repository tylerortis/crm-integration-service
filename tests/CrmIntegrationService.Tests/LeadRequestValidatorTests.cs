using CrmIntegrationService.Leads;

namespace CrmIntegrationService.Tests;

public class LeadRequestValidatorTests
{
    private readonly LeadRequestValidator _validator = new();

    [Theory]
    [InlineData("Ada Lovelace", "ada@example.com")]
    [InlineData(null, "ada@example.com")]
    [InlineData("", "  grace@example.com ")]
    public void Accepts_valid_requests(string? name, string email)
    {
        Assert.True(_validator.Validate(new LeadRequest(name, email)).IsValid);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not-an-email")]
    [InlineData("Ada <ada@example.com>")]
    public void Rejects_missing_or_malformed_email(string? email)
    {
        var result = _validator.Validate(new LeadRequest("Ada", email));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LeadRequest.Email));
    }

    [Fact]
    public void Rejects_email_longer_than_254_characters()
    {
        var email = new string('a', 250) + "@example.com";

        Assert.False(_validator.Validate(new LeadRequest(null, email)).IsValid);
    }

    [Fact]
    public void Rejects_name_longer_than_100_characters()
    {
        var result = _validator.Validate(new LeadRequest(new string('n', 101), "ada@example.com"));

        Assert.Contains(result.Errors, e => e.PropertyName == nameof(LeadRequest.Name));
    }
}
