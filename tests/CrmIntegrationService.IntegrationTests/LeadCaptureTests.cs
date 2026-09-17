using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using CrmIntegrationService.Data;
using CrmIntegrationService.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CrmIntegrationService.IntegrationTests;

public class LeadCaptureTests(LeadApiFactory factory) : IClassFixture<LeadApiFactory>
{
    private sealed record LeadBody(Guid LeadId, string DownloadUrl, DateTimeOffset DownloadExpiresAt);

    private static string UniqueEmail() => $"reader-{Guid.NewGuid():N}@example.com";

    private async Task<Lead?> LoadLeadAsync(Guid id)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<LeadsDbContext>().Leads.AsNoTracking().SingleOrDefaultAsync(l => l.Id == id);
    }

    [Fact]
    public async Task Opt_in_records_lead_forwards_it_to_the_crm_and_returns_a_working_download_link()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();

        var response = await client.PostAsJsonAsync($"/offers/{LeadApiFactory.LiveSlug}/leads", new { name = "Ada", email });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = (await response.Content.ReadFromJsonAsync<LeadBody>())!;

        var forwarded = await Eventually.GetAsync(async () =>
            await LoadLeadAsync(body.LeadId) is { ForwardStatus: ForwardStatus.Forwarded } lead ? lead : null);
        Assert.Equal(email, forwarded.Email);

        var crmCall = Assert.Single(factory.Crm.Requests, r => r.Body!.Contains(email, StringComparison.Ordinal));
        Assert.Equal(body.LeadId.ToString("N"), crmCall.Headers["Idempotency-Key"]);
        using var crmJson = JsonDocument.Parse(crmCall.Body!);
        Assert.Equal(LeadApiFactory.LiveSlug, crmJson.RootElement.GetProperty("offerSlug").GetString());

        var download = await client.GetAsync(body.DownloadUrl);
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("application/pdf", download.Content.Headers.ContentType!.MediaType);
        Assert.StartsWith("%PDF", Encoding.ASCII.GetString(await download.Content.ReadAsByteArrayAsync()));
    }

    [Fact]
    public async Task Repeat_opt_in_returns_the_existing_lead()
    {
        var client = factory.CreateClient();
        var email = UniqueEmail();

        var first = await client.PostAsJsonAsync($"/offers/{LeadApiFactory.LiveSlug}/leads", new { email });
        var second = await client.PostAsJsonAsync($"/offers/{LeadApiFactory.LiveSlug}/leads", new { email = email.ToUpperInvariant() });

        Assert.Equal(HttpStatusCode.Accepted, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.Equal((await first.Content.ReadFromJsonAsync<LeadBody>())!.LeadId, (await second.Content.ReadFromJsonAsync<LeadBody>())!.LeadId);
    }

    [Fact]
    public async Task Invalid_request_returns_validation_problem()
    {
        var response = await factory.CreateClient().PostAsJsonAsync($"/offers/{LeadApiFactory.LiveSlug}/leads", new { email = "not-an-email" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Email", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Opt_in_for_unknown_offer_returns_404()
    {
        var response = await factory.CreateClient().PostAsJsonAsync("/offers/no-such-offer/leads", new { email = UniqueEmail() });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Tampered_download_token_returns_404()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync($"/offers/{LeadApiFactory.LiveSlug}/leads", new { email = UniqueEmail() });
        var url = (await response.Content.ReadFromJsonAsync<LeadBody>())!.DownloadUrl;

        var index = url.LastIndexOf('.') + 10;
        var tampered = url[..index] + (url[index] == 'A' ? 'B' : 'A') + url[(index + 1)..];

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync(tampered)).StatusCode);
    }
}
