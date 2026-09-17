using System.Net;
using System.Security.Cryptography;
using CrmIntegrationService.Crm;
using CrmIntegrationService.Offers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.DependencyInjection;

namespace CrmIntegrationService.IntegrationTests;

public sealed class LeadApiFactory : WebApplicationFactory<Program>
{
    public const string LiveSlug = "starter-checklist";

    private readonly string _databasePath = Path.Combine(Path.GetTempPath(), $"crm-integration-tests-{Guid.NewGuid():N}.db");

    public StubHttpHandler TableStore { get; } = new(RespondWithOffers);

    public StubHttpHandler Crm { get; } = new(_ => new HttpResponseMessage(HttpStatusCode.OK));

    private static HttpResponseMessage RespondWithOffers(HttpRequestMessage request)
    {
        var query = Uri.UnescapeDataString(request.RequestUri!.Query);
        var json = query.Contains($"{{Slug}}=\"{LiveSlug}\"", StringComparison.Ordinal)
            ? $$$"""{"records":[{"id":"row_1","fields":{"Slug":"{{{LiveSlug}}}","Title":"Starter Checklist","Summary":"Synthetic test offer.","Status":"Live"}}]}"""
            : """{"records":[]}""";
        return StubHttpHandler.Json(json);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Leads", $"Data Source={_databasePath}");
        builder.UseSetting("TableStore:Mode", "Http");
        builder.UseSetting("TableStore:BaseUrl", "https://tables.example.test");
        builder.UseSetting("TableStore:ApiKey", "placeholder-api-key");
        builder.UseSetting("TableStore:DatabaseId", "db_test");
        builder.UseSetting("Crm:WebhookUrl", "https://crm.example.test/hooks/leads");
        builder.UseSetting("Crm:MaxForwardAttempts", "100");
        builder.UseSetting("Crm:SweepInterval", "00:00:00.100");
        builder.UseSetting("Crm:RetryBaseDelay", "00:00:00.100");
        builder.UseSetting("Crm:RetryMaxDelay", "00:00:00.300");
        builder.UseSetting("Crm:Resilience:MaxRetryAttempts", "1");
        builder.UseSetting("Crm:Resilience:RetryDelay", "00:00:00.010");
        builder.UseSetting("Crm:Resilience:MinimumThroughput", "2");
        builder.UseSetting("Crm:Resilience:SamplingDuration", "00:00:01");
        builder.UseSetting("Crm:Resilience:BreakDuration", "00:00:00.500");
        builder.UseSetting("Downloads:SigningKey", Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)));

        builder.ConfigureTestServices(services =>
        {
            services.AddHttpClient<TableStoreClient>().ConfigurePrimaryHttpMessageHandler(() => TableStore);
            services.AddHttpClient<ICrmClient, CrmClient>().ConfigurePrimaryHttpMessageHandler(() => Crm);
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        SqliteConnection.ClearAllPools();
        File.Delete(_databasePath);
    }
}
