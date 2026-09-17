using CrmIntegrationService.Offers;
using Microsoft.Extensions.Caching.Memory;

namespace CrmIntegrationService.Tests;

public class OfferCatalogTests
{
    private sealed class CountingClient(Offer? result) : ITableStoreClient
    {
        public int Calls { get; private set; }

        public Task<Offer?> FindLiveOfferAsync(string slug, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(result);
        }
    }

    [Fact]
    public async Task Caches_found_offers()
    {
        var client = new CountingClient(new Offer("starter-checklist", "Starter Checklist", ""));
        var catalog = new OfferCatalog(client, new MemoryCache(new MemoryCacheOptions()));

        await catalog.FindAsync("starter-checklist", CancellationToken.None);
        var second = await catalog.FindAsync("starter-checklist", CancellationToken.None);

        Assert.Equal("Starter Checklist", second!.Title);
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Caches_misses_too()
    {
        var client = new CountingClient(null);
        var catalog = new OfferCatalog(client, new MemoryCache(new MemoryCacheOptions()));

        Assert.Null(await catalog.FindAsync("missing-offer", CancellationToken.None));
        Assert.Null(await catalog.FindAsync("missing-offer", CancellationToken.None));
        Assert.Equal(1, client.Calls);
    }

    [Fact]
    public async Task Invalid_slugs_are_rejected_without_a_lookup()
    {
        var client = new CountingClient(null);
        var catalog = new OfferCatalog(client, new MemoryCache(new MemoryCacheOptions()));

        Assert.Null(await catalog.FindAsync("Not A Slug", CancellationToken.None));
        Assert.Equal(0, client.Calls);
    }
}
