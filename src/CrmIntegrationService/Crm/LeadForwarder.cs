using CrmIntegrationService.Data;
using CrmIntegrationService.Leads;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CrmIntegrationService.Crm;

public sealed class LeadForwarder(
    LeadsDbContext db,
    ICrmClient crm,
    IOptions<CrmOptions> options,
    TimeProvider time,
    ILogger<LeadForwarder> logger)
{
    private const int MaxErrorLength = 500;

    public async Task<int> ForwardDueLeadsAsync(int batchSize, CancellationToken cancellationToken)
    {
        var now = time.GetUtcNow();
        var due = await db.Leads
            .Where(l => l.ForwardStatus == ForwardStatus.Pending && l.NextAttemptAt <= now)
            .OrderBy(l => l.NextAttemptAt)
            .Take(batchSize)
            .ToListAsync(cancellationToken);

        foreach (var lead in due)
        {
            await ForwardAsync(lead, cancellationToken);
            await db.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    public static TimeSpan BackoffFor(int attempts, CrmOptions options)
    {
        var multiplier = Math.Pow(2, Math.Clamp(attempts - 1, 0, 30));
        var ticks = Math.Min(options.RetryBaseDelay.Ticks * multiplier, options.RetryMaxDelay.Ticks);
        return TimeSpan.FromTicks((long)ticks);
    }

    private async Task ForwardAsync(Lead lead, CancellationToken cancellationToken)
    {
        lead.ForwardAttempts++;
        try
        {
            await crm.SendLeadAsync(new CrmLeadPayload(lead.Id, lead.Email, lead.Name, lead.OfferSlug, lead.CreatedAt), cancellationToken);
            lead.ForwardStatus = ForwardStatus.Forwarded;
            lead.ForwardedAt = time.GetUtcNow();
            lead.LastError = null;
            logger.LogInformation("Forwarded lead {LeadId} to CRM after {Attempts} attempt(s)", lead.Id, lead.ForwardAttempts);
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            lead.LastError = ex.Message.Length > MaxErrorLength ? ex.Message[..MaxErrorLength] : ex.Message;

            if (lead.ForwardAttempts >= options.Value.MaxForwardAttempts)
            {
                lead.ForwardStatus = ForwardStatus.Failed;
                logger.LogError(ex, "Giving up on lead {LeadId} after {Attempts} attempts", lead.Id, lead.ForwardAttempts);
            }
            else
            {
                lead.NextAttemptAt = time.GetUtcNow() + BackoffFor(lead.ForwardAttempts, options.Value);
                logger.LogWarning("CRM forward for lead {LeadId} failed (attempt {Attempts}); retrying at {NextAttemptAt}: {Error}",
                    lead.Id, lead.ForwardAttempts, lead.NextAttemptAt, lead.LastError);
            }
        }
    }
}
