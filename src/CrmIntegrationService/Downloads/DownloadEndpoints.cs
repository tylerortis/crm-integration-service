using CrmIntegrationService.Offers;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.Extensions.Options;

namespace CrmIntegrationService.Downloads;

public static class DownloadEndpoints
{
    public static IEndpointRouteBuilder MapDownloadEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/downloads/{token}", async Task<Results<PhysicalFileHttpResult, NotFound, StatusCodeHttpResult>> (
                string token,
                DownloadTokenService tokens,
                OfferCatalog offers,
                IOptions<DownloadOptions> options,
                IWebHostEnvironment environment,
                CancellationToken cancellationToken) =>
            {
                var result = tokens.Validate(token);
                if (result.Status == TokenStatus.Expired)
                {
                    return TypedResults.StatusCode(StatusCodes.Status410Gone);
                }

                if (result.Status != TokenStatus.Valid || await offers.FindAsync(result.OfferSlug!, cancellationToken) is not { } offer)
                {
                    return TypedResults.NotFound();
                }

                // offer.Slug passed Slug.IsValid, so it can't contain path separators.
                var fileName = $"{offer.Slug}.pdf";
                var path = Path.Combine(environment.ContentRootPath, options.Value.AssetDirectory, fileName);
                return File.Exists(path)
                    ? TypedResults.PhysicalFile(path, "application/pdf", fileName)
                    : TypedResults.NotFound();
            })
            .WithName("DownloadAsset")
            .WithSummary("Download an offer asset with a signed, expiring token.");

        return app;
    }
}
