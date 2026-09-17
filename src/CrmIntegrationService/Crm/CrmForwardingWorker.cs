using Microsoft.Extensions.Options;

namespace CrmIntegrationService.Crm;

/// <summary>
/// Forwards due leads whenever a new lead is captured, and on a timer so that retries happen
/// even when no new leads arrive. Leads live in the database, so a restart resumes where it left off.
/// </summary>
public sealed class CrmForwardingWorker(
    IServiceScopeFactory scopeFactory,
    IForwardSignal signal,
    IOptions<CrmOptions> options,
    ILogger<CrmForwardingWorker> logger) : BackgroundService
{
    private const int BatchSize = 50;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                int processed;
                do
                {
                    await using var scope = scopeFactory.CreateAsyncScope();
                    processed = await scope.ServiceProvider.GetRequiredService<LeadForwarder>()
                        .ForwardDueLeadsAsync(BatchSize, stoppingToken);
                }
                while (processed == BatchSize);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                logger.LogError(ex, "CRM forwarding sweep failed");
            }

            try
            {
                await signal.WaitAsync(options.Value.SweepInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
