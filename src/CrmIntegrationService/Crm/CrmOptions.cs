namespace CrmIntegrationService.Crm;

public sealed class CrmOptions
{
    public const string SectionName = "Crm";

    public string WebhookUrl { get; set; } = "";

    /// <summary>Background forwarding attempts before a lead is marked Failed.</summary>
    public int MaxForwardAttempts { get; set; } = 8;

    public TimeSpan SweepInterval { get; set; } = TimeSpan.FromSeconds(15);

    public TimeSpan RetryBaseDelay { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan RetryMaxDelay { get; set; } = TimeSpan.FromMinutes(15);

    public CrmResilienceOptions Resilience { get; set; } = new();

    public bool IsValid() =>
        Uri.TryCreate(WebhookUrl, UriKind.Absolute, out var uri)
        && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
        && MaxForwardAttempts >= 1
        && SweepInterval > TimeSpan.Zero
        && RetryBaseDelay > TimeSpan.Zero
        && RetryMaxDelay >= RetryBaseDelay;
}

/// <summary>Per-call HTTP resilience (seconds scale). Background retries (minutes scale) live in CrmOptions.</summary>
public sealed class CrmResilienceOptions
{
    public int MaxRetryAttempts { get; set; } = 3;

    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromSeconds(1);

    public TimeSpan AttemptTimeout { get; set; } = TimeSpan.FromSeconds(10);

    public TimeSpan TotalTimeout { get; set; } = TimeSpan.FromSeconds(60);

    public double FailureRatio { get; set; } = 0.5;

    public int MinimumThroughput { get; set; } = 5;

    public TimeSpan SamplingDuration { get; set; } = TimeSpan.FromSeconds(30);

    public TimeSpan BreakDuration { get; set; } = TimeSpan.FromSeconds(30);
}
