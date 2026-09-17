using CrmIntegrationService.Crm;
using CrmIntegrationService.Data;
using CrmIntegrationService.Leads;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace CrmIntegrationService.Tests;

public sealed class LeadForwarderTests : IDisposable
{
    private static readonly DateTimeOffset Start = new(2026, 5, 4, 9, 0, 0, TimeSpan.Zero);

    private readonly SqliteConnection _connection = new("DataSource=:memory:");
    private readonly ManualTimeProvider _time = new(Start);
    private readonly FakeCrm _crm = new();
    private readonly CrmOptions _options = new()
    {
        WebhookUrl = "https://crm.example.test/hooks/leads",
        MaxForwardAttempts = 3,
        RetryBaseDelay = TimeSpan.FromMinutes(1),
        RetryMaxDelay = TimeSpan.FromMinutes(3),
    };

    public LeadForwarderTests()
    {
        _connection.Open();
        using var db = NewDb();
        db.Database.EnsureCreated();
    }

    public void Dispose() => _connection.Dispose();

    private LeadsDbContext NewDb() => new(new DbContextOptionsBuilder<LeadsDbContext>().UseSqlite(_connection).Options);

    private LeadForwarder NewForwarder(LeadsDbContext db) =>
        new(db, _crm, Options.Create(_options), _time, NullLogger<LeadForwarder>.Instance);

    private async Task<Guid> SeedLeadAsync(string email, DateTimeOffset? nextAttemptAt = null)
    {
        await using var db = NewDb();
        var lead = new Lead
        {
            Id = Guid.NewGuid(),
            OfferSlug = "starter-checklist",
            Email = email,
            CreatedAt = Start,
            ForwardStatus = ForwardStatus.Pending,
            NextAttemptAt = nextAttemptAt ?? Start,
        };
        db.Leads.Add(lead);
        await db.SaveChangesAsync();
        return lead.Id;
    }

    private async Task<Lead> LoadAsync(Guid id)
    {
        await using var db = NewDb();
        return await db.Leads.SingleAsync(l => l.Id == id);
    }

    private async Task<int> RunAsync()
    {
        await using var db = NewDb();
        return await NewForwarder(db).ForwardDueLeadsAsync(50, CancellationToken.None);
    }

    [Fact]
    public async Task Successful_forward_marks_lead_forwarded()
    {
        var id = await SeedLeadAsync("ada@example.com");

        Assert.Equal(1, await RunAsync());

        var lead = await LoadAsync(id);
        Assert.Equal(ForwardStatus.Forwarded, lead.ForwardStatus);
        Assert.Equal(1, lead.ForwardAttempts);
        Assert.Equal(Start, lead.ForwardedAt);
        Assert.Equal(id, Assert.Single(_crm.Sent).LeadId);
    }

    [Fact]
    public async Task Failed_forward_stays_pending_with_backoff()
    {
        _crm.FailWith = new HttpRequestException("crm down");
        var id = await SeedLeadAsync("grace@example.com");

        await RunAsync();

        var lead = await LoadAsync(id);
        Assert.Equal(ForwardStatus.Pending, lead.ForwardStatus);
        Assert.Equal(1, lead.ForwardAttempts);
        Assert.Equal(Start.AddMinutes(1), lead.NextAttemptAt);
        Assert.Equal("crm down", lead.LastError);
    }

    [Fact]
    public async Task Leads_not_yet_due_are_skipped_until_their_time()
    {
        await SeedLeadAsync("later@example.com", Start.AddMinutes(5));

        Assert.Equal(0, await RunAsync());
        _time.Advance(TimeSpan.FromMinutes(5));
        Assert.Equal(1, await RunAsync());
    }

    [Fact]
    public async Task Lead_is_marked_failed_after_max_attempts_and_is_not_retried_again()
    {
        _crm.FailWith = new HttpRequestException("still down");
        var id = await SeedLeadAsync("linus@example.com");

        for (var i = 0; i < 3; i++)
        {
            await RunAsync();
            _time.Advance(TimeSpan.FromHours(1));
        }

        var lead = await LoadAsync(id);
        Assert.Equal(ForwardStatus.Failed, lead.ForwardStatus);
        Assert.Equal(3, lead.ForwardAttempts);
        Assert.Equal(0, await RunAsync());
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(2, 2)]
    [InlineData(3, 3)]
    [InlineData(10, 3)]
    public void Backoff_doubles_and_is_capped(int attempts, int expectedMinutes)
    {
        Assert.Equal(TimeSpan.FromMinutes(expectedMinutes), LeadForwarder.BackoffFor(attempts, _options));
    }

    private sealed class FakeCrm : ICrmClient
    {
        public Exception? FailWith { get; set; }

        public List<CrmLeadPayload> Sent { get; } = [];

        public Task SendLeadAsync(CrmLeadPayload payload, CancellationToken cancellationToken)
        {
            if (FailWith is not null)
            {
                return Task.FromException(FailWith);
            }

            Sent.Add(payload);
            return Task.CompletedTask;
        }
    }
}
