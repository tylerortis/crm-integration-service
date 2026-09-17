namespace CrmIntegrationService.Crm;

public sealed record CrmLeadPayload(Guid LeadId, string Email, string? Name, string OfferSlug, DateTimeOffset CapturedAt);

public interface ICrmClient
{
    /// <summary>Delivers a lead to the CRM webhook. Throws if delivery ultimately fails.</summary>
    Task SendLeadAsync(CrmLeadPayload payload, CancellationToken cancellationToken);
}
