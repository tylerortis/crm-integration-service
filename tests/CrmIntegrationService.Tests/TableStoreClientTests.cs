using System.Net;
using CrmIntegrationService.Offers;
using Microsoft.Extensions.Options;

namespace CrmIntegrationService.Tests;

public class TableStoreClientTests
{
    private const string OneRecord = """
        {"records":[{"id":"row_1","fields":{"Slug":"starter-checklist","Title":"Starter Checklist","Summary":"A synthetic sample offer.","Status":"Live"}}]}
        """;

    private static (TableStoreClient Client, StubHttpHandler Handler) Create(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHttpHandler(respond);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://tables.example.test/") };
        var options = Options.Create(new TableStoreOptions
        {
            BaseUrl = "https://tables.example.test",
            ApiKey = "placeholder-api-key",
            DatabaseId = "db_demo",
            Table = "Offers",
        });
        return (new TableStoreClient(http, options), handler);
    }

    [Fact]
    public async Task Queries_live_offer_by_slug_with_bearer_auth_and_maps_fields()
    {
        var (client, handler) = Create(_ => StubHttpHandler.Json(OneRecord));

        var offer = await client.FindLiveOfferAsync("starter-checklist", CancellationToken.None);

        Assert.Equal(new Offer("starter-checklist", "Starter Checklist", "A synthetic sample offer."), offer);
        var request = Assert.Single(handler.Requests);
        Assert.Equal("/v0/db_demo/Offers", request.Uri.AbsolutePath);
        var query = Uri.UnescapeDataString(request.Uri.Query);
        Assert.Contains("filterByFormula=AND({Slug}=\"starter-checklist\",{Status}=\"Live\")", query);
        Assert.Contains("maxRecords=1", query);
        Assert.Equal("Bearer placeholder-api-key", request.Headers["Authorization"]);
    }

    [Fact]
    public async Task Returns_null_when_no_record_matches()
    {
        var (client, _) = Create(_ => StubHttpHandler.Json("""{"records":[]}"""));

        Assert.Null(await client.FindLiveOfferAsync("missing-offer", CancellationToken.None));
    }

    [Fact]
    public async Task Invalid_slug_never_reaches_the_table_store()
    {
        var (client, handler) = Create(_ => StubHttpHandler.Json(OneRecord));

        Assert.Null(await client.FindLiveOfferAsync("x\",TRUE()) OR (\"", CancellationToken.None));
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Upstream_errors_surface_as_exceptions()
    {
        var (client, _) = Create(_ => StubHttpHandler.Json("{}", HttpStatusCode.Unauthorized));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.FindLiveOfferAsync("starter-checklist", CancellationToken.None));
    }
}
