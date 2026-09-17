using Microsoft.AspNetCore.Http.HttpResults;

namespace CrmIntegrationService.Offers;

public sealed record OfferResponse(string Slug, string Title, string Summary);

public static class OfferEndpoints
{
    public static IEndpointRouteBuilder MapOfferEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/offers/{slug}", async Task<Results<Ok<OfferResponse>, NotFound>> (
                string slug, OfferCatalog offers, CancellationToken cancellationToken) =>
                await offers.FindAsync(slug, cancellationToken) is { } offer
                    ? TypedResults.Ok(new OfferResponse(offer.Slug, offer.Title, offer.Summary))
                    : TypedResults.NotFound())
            .WithName("GetOffer")
            .WithSummary("Look up a live offer by slug.");

        return app;
    }
}
