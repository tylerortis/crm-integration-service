using CrmIntegrationService.Offers;

namespace CrmIntegrationService.Tests;

public class SlugTests
{
    [Theory]
    [InlineData("starter-checklist", true)]
    [InlineData("a", true)]
    [InlineData("guide-2026", true)]
    [InlineData("", false)]
    [InlineData(null, false)]
    [InlineData("-leading", false)]
    [InlineData("trailing-", false)]
    [InlineData("Upper-Case", false)]
    [InlineData("quote\"injection", false)]
    [InlineData("../etc/passwd", false)]
    public void Validates_slug_format(string? slug, bool expected) => Assert.Equal(expected, Slug.IsValid(slug));

    [Fact]
    public void Rejects_slugs_longer_than_64_characters()
    {
        Assert.True(Slug.IsValid(new string('a', 64)));
        Assert.False(Slug.IsValid(new string('a', 65)));
    }
}
