using CrmIntegrationService.Crm;
using CrmIntegrationService.Data;
using CrmIntegrationService.Development;
using CrmIntegrationService.Downloads;
using CrmIntegrationService.Leads;
using CrmIntegrationService.Offers;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

if (!builder.Environment.IsDevelopment())
{
    builder.Logging.ClearProviders();
    builder.Logging.AddJsonConsole();
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddMemoryCache();
builder.Services.AddProblemDetails();

builder.Services.AddOptions<TableStoreOptions>()
    .Bind(builder.Configuration.GetSection(TableStoreOptions.SectionName))
    .Validate(o => o.IsValid(), "TableStore requires BaseUrl, ApiKey, DatabaseId and Table unless Mode is Sample.")
    .ValidateOnStart();
builder.Services.AddOptions<CrmOptions>()
    .Bind(builder.Configuration.GetSection(CrmOptions.SectionName))
    .Validate(o => o.IsValid(), "Crm requires an absolute WebhookUrl and positive retry settings.")
    .ValidateOnStart();
builder.Services.AddOptions<DownloadOptions>()
    .Bind(builder.Configuration.GetSection(DownloadOptions.SectionName))
    .Validate(o => DownloadOptions.HasValidKey(o.SigningKey), "Downloads:SigningKey must be base64 and at least 32 bytes.")
    .Validate(o => o.TokenLifetime > TimeSpan.Zero, "Downloads:TokenLifetime must be positive.")
    .ValidateOnStart();

builder.Services.AddDbContext<LeadsDbContext>((services, options) =>
    options.UseSqlite(services.GetRequiredService<IConfiguration>().GetConnectionString("Leads")
        ?? throw new InvalidOperationException("ConnectionStrings:Leads is not configured.")));

// Offers
builder.Services.AddHttpClient<TableStoreClient>((services, client) =>
    {
        var o = services.GetRequiredService<IOptions<TableStoreOptions>>().Value;
        if (o.Mode == TableStoreMode.Http)
        {
            client.BaseAddress = new Uri(o.BaseUrl.TrimEnd('/') + "/");
        }
    })
    .AddStandardResilienceHandler();
builder.Services.AddSingleton<SampleTableStoreClient>();
builder.Services.AddScoped<ITableStoreClient>(services =>
    services.GetRequiredService<IOptions<TableStoreOptions>>().Value.Mode == TableStoreMode.Sample
        ? services.GetRequiredService<SampleTableStoreClient>()
        : services.GetRequiredService<TableStoreClient>());
builder.Services.AddScoped<OfferCatalog>();

// Leads and CRM forwarding
builder.Services.AddSingleton<IValidator<LeadRequest>, LeadRequestValidator>();
builder.Services.AddScoped<LeadService>();
builder.Services.AddSingleton<IForwardSignal, ForwardSignal>();
builder.Services.AddCrmClient();
builder.Services.AddScoped<LeadForwarder>();
builder.Services.AddHostedService<CrmForwardingWorker>();

// Downloads
builder.Services.AddSingleton<DownloadTokenService>();

builder.Services.AddHealthChecks().AddDbContextCheck<LeadsDbContext>();
builder.Services.AddOpenApi();

var app = builder.Build();

// Demo-friendly schema bootstrap; see README "Design decisions".
await using (var scope = app.Services.CreateAsyncScope())
{
    await scope.ServiceProvider.GetRequiredService<LeadsDbContext>().Database.EnsureCreatedAsync();
}

app.UseExceptionHandler();
app.MapOpenApi();
app.MapHealthChecks("/health");
app.MapOfferEndpoints();
app.MapLeadEndpoints();
app.MapDownloadEndpoints();

if (app.Environment.IsDevelopment())
{
    app.MapDevCrmSink();
}

app.Run();
