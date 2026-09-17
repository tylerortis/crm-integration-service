namespace CrmIntegrationService.Leads;

public enum ForwardStatus
{
    Pending,
    Forwarded,
    Failed,
}

public sealed class Lead
{
    public Guid Id { get; set; }

    public required string OfferSlug { get; set; }

    public string? Name { get; set; }

    /// <summary>Trimmed and lower-cased; unique per offer.</summary>
    public required string Email { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public ForwardStatus ForwardStatus { get; set; }

    public int ForwardAttempts { get; set; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? ForwardedAt { get; set; }

    public string? LastError { get; set; }
}
