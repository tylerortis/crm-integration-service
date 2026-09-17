using System.Net;
using System.Net.Http.Json;

namespace CrmIntegrationService.IntegrationTests;

public class OfferEndpointTests(LeadApiFactory factory) : IClassFixture<LeadApiFactory>
{
    private sealed record OfferBody(string Slug, string Title, string Summary);

    [Fact]
    public async Task Live_offer_is_served_from_the_table_store()
    {
        var response = await factory.CreateClient().GetAsync($"/offers/{LeadApiFactory.LiveSlug}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(new OfferBody(LeadApiFactory.LiveSlug, "Starter Checklist", "Synthetic test offer."),
            await response.Content.ReadFromJsonAsync<OfferBody>());
        Assert.Contains(factory.TableStore.Requests, r => r.Headers["Authorization"] == "Bearer placeholder-api-key");
    }

    [Fact]
    public async Task Unknown_offer_returns_404()
    {
        var response = await factory.CreateClient().GetAsync("/offers/no-such-offer");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Malformed_slug_returns_404_without_calling_the_table_store()
    {
        var before = factory.TableStore.Requests.Count;

        var response = await factory.CreateClient().GetAsync("/offers/Bad%22Slug");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal(before, factory.TableStore.Requests.Count);
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy()
    {
        var response = await factory.CreateClient().GetAsync("/health");

        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }
}
