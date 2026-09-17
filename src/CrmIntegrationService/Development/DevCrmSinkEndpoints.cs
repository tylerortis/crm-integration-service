using System.Text.Json;

namespace CrmIntegrationService.Development;

/// <summary>Local stand-in for a CRM webhook so the app runs end to end with no external accounts.</summary>
public static class DevCrmSinkEndpoints
{
    public static IEndpointRouteBuilder MapDevCrmSink(this IEndpointRouteBuilder app)
    {
        app.MapPost("/dev/crm-sink", (JsonElement payload, HttpRequest request, ILoggerFactory loggers) =>
            {
                loggers.CreateLogger("DevCrmSink").LogInformation(
                    "CRM sink received lead (Idempotency-Key {Key}): {Payload}",
                    request.Headers["Idempotency-Key"].ToString(), payload.GetRawText());
                return Results.NoContent();
            })
            .ExcludeFromDescription();

        return app;
    }
}
