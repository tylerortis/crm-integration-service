namespace CrmIntegrationService.Offers;

public interface ITableStoreClient
{
    /// <summary>Returns the offer with this slug if it exists and is live; otherwise null.</summary>
    Task<Offer?> FindLiveOfferAsync(string slug, CancellationToken cancellationToken);
}
