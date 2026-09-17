namespace CrmIntegrationService.Downloads;

public sealed class DownloadOptions
{
    public const string SectionName = "Downloads";

    /// <summary>Base64 HMAC key, at least 32 bytes. Supply via user-secrets or environment.</summary>
    public string SigningKey { get; set; } = "";

    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);

    public string AssetDirectory { get; set; } = "assets/offers";

    public static bool HasValidKey(string? key)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        Span<byte> buffer = stackalloc byte[512];
        return Convert.TryFromBase64String(key, buffer, out var written) && written >= 32;
    }
}
