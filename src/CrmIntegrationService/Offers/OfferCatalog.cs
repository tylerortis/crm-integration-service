using Microsoft.Extensions.Caching.Memory;

namespace CrmIntegrationService.Offers;

public sealed class OfferCatalog(ITableStoreClient client, IMemoryCache cache)
{
    public static readonly TimeSpan HitLifetime = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MissLifetime = TimeSpan.FromSeconds(30);

    public async Task<Offer?> FindAsync(string slug, CancellationToken cancellationToken)
    {
        if (!Slug.IsValid(slug))
        {
            return null;
        }

        var key = $"offer:{slug}";
        if (cache.TryGetValue(key, out Offer? cached))
        {
            return cached;
        }

        var offer = await client.FindLiveOfferAsync(slug, cancellationToken);
        cache.Set(key, offer, offer is null ? MissLifetime : HitLifetime);
        return offer;
    }
}
