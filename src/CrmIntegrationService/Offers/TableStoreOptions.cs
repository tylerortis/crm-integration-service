namespace CrmIntegrationService.Offers;

public enum TableStoreMode
{
    /// <summary>Query a real table-store REST API.</summary>
    Http,

    /// <summary>Serve a built-in synthetic offer (local development without credentials).</summary>
    Sample,
}

public sealed class TableStoreOptions
{
    public const string SectionName = "TableStore";

    public TableStoreMode Mode { get; set; } = TableStoreMode.Http;

    public string BaseUrl { get; set; } = "";

    public string ApiKey { get; set; } = "";

    public string DatabaseId { get; set; } = "";

    public string Table { get; set; } = "Offers";

    public bool IsValid() =>
        Mode == TableStoreMode.Sample
        || (Uri.TryCreate(BaseUrl, UriKind.Absolute, out var uri)
            && (uri.Scheme == Uri.UriSchemeHttps || uri.Scheme == Uri.UriSchemeHttp)
            && !string.IsNullOrWhiteSpace(ApiKey)
            && !string.IsNullOrWhiteSpace(DatabaseId)
            && !string.IsNullOrWhiteSpace(Table));
}
