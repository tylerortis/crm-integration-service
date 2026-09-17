using CrmIntegrationService.Downloads;
using CrmIntegrationService.Offers;
using FluentValidation;
using Microsoft.AspNetCore.Http.HttpResults;

namespace CrmIntegrationService.Leads;

public sealed record LeadResponse(Guid LeadId, string DownloadUrl, DateTimeOffset DownloadExpiresAt);

public static class LeadEndpoints
{
    public static IEndpointRouteBuilder MapLeadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/offers/{slug}/leads", async Task<Results<Accepted<LeadResponse>, Ok<LeadResponse>, ValidationProblem, NotFound>> (
                string slug,
                LeadRequest request,
                IValidator<LeadRequest> validator,
                OfferCatalog offers,
                LeadService leads,
                DownloadTokenService tokens,
                CancellationToken cancellationToken) =>
            {
                var validation = await validator.ValidateAsync(request, cancellationToken);
                if (!validation.IsValid)
                {
                    return TypedResults.ValidationProblem(validation.ToDictionary());
                }

                if (await offers.FindAsync(slug, cancellationToken) is not { } offer)
                {
                    return TypedResults.NotFound();
                }

                var accepted = await leads.CaptureAsync(offer, request, cancellationToken);
                var issued = tokens.Issue(offer.Slug, accepted.LeadId);
                var response = new LeadResponse(accepted.LeadId, $"/downloads/{issued.Token}", issued.ExpiresAt);

                return accepted.Created
                    ? TypedResults.Accepted((string?)null, response)
                    : TypedResults.Ok(response);
            })
            .WithName("CaptureLead")
            .WithSummary("Record an opt-in, queue it for the CRM, and return a signed download link.");

        return app;
    }
}
