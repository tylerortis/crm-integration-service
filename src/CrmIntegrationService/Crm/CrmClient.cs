using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace CrmIntegrationService.Crm;

public sealed class CrmClient(HttpClient http, IOptions<CrmOptions> options) : ICrmClient
{
    public async Task SendLeadAsync(CrmLeadPayload payload, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, options.Value.WebhookUrl)
        {
            Content = JsonContent.Create(payload),
        };

        // Same key on every retry and every background attempt, so the receiver can drop duplicates.
        request.Headers.Add("Idempotency-Key", payload.LeadId.ToString("N"));

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
