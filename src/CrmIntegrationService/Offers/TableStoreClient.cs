using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.Options;

namespace CrmIntegrationService.Offers;

/// <summary>
/// Reads offers from a table-store REST API shaped like
/// GET /v0/{database}/{table}?filterByFormula=...&amp;maxRecords=1 → { "records": [ { "fields": { ... } } ] }.
/// </summary>
public sealed class TableStoreClient(HttpClient http, IOptions<TableStoreOptions> options) : ITableStoreClient
{
    public async Task<Offer?> FindLiveOfferAsync(string slug, CancellationToken cancellationToken)
    {
        // Slug.IsValid only admits [a-z0-9-], so the value can't break out of the formula string.
        if (!Slug.IsValid(slug))
        {
            return null;
        }

        var o = options.Value;
        var formula = $"AND({{Slug}}=\"{slug}\",{{Status}}=\"Live\")";
        var path = $"v0/{Uri.EscapeDataString(o.DatabaseId)}/{Uri.EscapeDataString(o.Table)}"
            + $"?filterByFormula={Uri.EscapeDataString(formula)}&maxRecords=1";

        using var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", o.ApiKey);

        using var response = await http.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var page = await response.Content.ReadFromJsonAsync<RecordPage>(cancellationToken);
        var fields = page?.Records?.FirstOrDefault()?.Fields;
        if (fields is not { Slug: { } foundSlug, Title: { } title })
        {
            return null;
        }

        return new Offer(foundSlug, title, fields.Summary ?? "");
    }

    private sealed record RecordPage(List<TableRecord>? Records);

    private sealed record TableRecord(string? Id, OfferFields? Fields);

    private sealed record OfferFields(string? Slug, string? Title, string? Summary);
}
