using System.Text.RegularExpressions;

namespace CrmIntegrationService.Offers;

/// <summary>
/// URL slugs are the only user-controlled value that reaches the table-store filter formula
/// and the asset file path, so the strict format is the injection and path-traversal guard.
/// </summary>
public static partial class Slug
{
    public static bool IsValid(string? value) => value is not null && Pattern().IsMatch(value);

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]{0,62}[a-z0-9])?$")]
    private static partial Regex Pattern();
}
