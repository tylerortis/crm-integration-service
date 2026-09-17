namespace CrmIntegrationService.Offers;

/// <summary>In-memory stand-in used when TableStore:Mode is Sample.</summary>
public sealed class SampleTableStoreClient : ITableStoreClient
{
    private static readonly Offer[] Offers =
    [
        new("starter-checklist", "Starter Checklist", "A one-page synthetic checklist used to demo the download flow."),
    ];

    public Task<Offer?> FindLiveOfferAsync(string slug, CancellationToken cancellationToken) =>
        Task.FromResult(Offers.FirstOrDefault(o => o.Slug == slug));
}
