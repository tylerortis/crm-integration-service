using CrmIntegrationService.Crm;
using CrmIntegrationService.Data;
using CrmIntegrationService.Offers;
using Microsoft.EntityFrameworkCore;

namespace CrmIntegrationService.Leads;

public sealed record LeadAccepted(Guid LeadId, bool Created);

public sealed class LeadService(LeadsDbContext db, IForwardSignal forwardSignal, TimeProvider time)
{
    public async Task<LeadAccepted> CaptureAsync(Offer offer, LeadRequest request, CancellationToken cancellationToken)
    {
        var email = request.Email!.Trim().ToLowerInvariant();

        if (await FindExistingAsync(offer.Slug, email, cancellationToken) is { } existing)
        {
            return new LeadAccepted(existing, Created: false);
        }

        var now = time.GetUtcNow();
        var lead = new Lead
        {
            Id = Guid.CreateVersion7(now),
            OfferSlug = offer.Slug,
            Name = string.IsNullOrWhiteSpace(request.Name) ? null : request.Name.Trim(),
            Email = email,
            CreatedAt = now,
            ForwardStatus = ForwardStatus.Pending,
            NextAttemptAt = now,
        };
        db.Leads.Add(lead);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            // A concurrent opt-in for the same offer and email won the unique index.
            db.ChangeTracker.Clear();
            if (await FindExistingAsync(offer.Slug, email, cancellationToken) is { } winner)
            {
                return new LeadAccepted(winner, Created: false);
            }

            throw;
        }

        forwardSignal.Notify();
        return new LeadAccepted(lead.Id, Created: true);
    }

    private async Task<Guid?> FindExistingAsync(string offerSlug, string email, CancellationToken cancellationToken) =>
        await db.Leads
            .Where(l => l.OfferSlug == offerSlug && l.Email == email)
            .Select(l => (Guid?)l.Id)
            .FirstOrDefaultAsync(cancellationToken);
}
