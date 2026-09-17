using System.Net;
using System.Net.Http.Json;
using CrmIntegrationService.Data;
using CrmIntegrationService.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmIntegrationService.IntegrationTests;

public class CrmOutageRecoveryTests
{
    private sealed record LeadBody(Guid LeadId, string DownloadUrl);

    [Fact]
    public async Task Lead_captured_during_crm_outage_is_forwarded_after_recovery()
    {
        await using var factory = new LeadApiFactory();
        factory.Crm.Respond = _ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var client = factory.CreateClient();

        var response = await client.PostAsJsonAsync($"/offers/{LeadApiFactory.LiveSlug}/leads", new { email = "outage@example.com" });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<LeadBody>())!;

        async Task<Lead> LoadAsync()
        {
            await using var scope = factory.Services.CreateAsyncScope();
            return await scope.ServiceProvider.GetRequiredService<LeadsDbContext>().Leads.AsNoTracking().SingleAsync(l => l.Id == body.LeadId);
        }

        // The download link works even while the CRM is down.
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync(body.DownloadUrl)).StatusCode);

        var failing = await Eventually.GetAsync(async () => await LoadAsync() is { ForwardAttempts: >= 2 } lead ? lead : null);
        Assert.Equal(ForwardStatus.Pending, failing.ForwardStatus);
        Assert.NotNull(failing.LastError);

        factory.Crm.Respond = _ => new HttpResponseMessage(HttpStatusCode.OK);

        var recovered = await Eventually.GetAsync(async () => await LoadAsync() is { ForwardStatus: ForwardStatus.Forwarded } lead ? lead : null);
        Assert.Null(recovered.LastError);
        Assert.All(factory.Crm.Requests, r => Assert.Equal(body.LeadId.ToString("N"), r.Headers["Idempotency-Key"]));
    }
}
