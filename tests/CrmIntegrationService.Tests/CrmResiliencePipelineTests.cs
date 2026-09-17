using System.Net;
using CrmIntegrationService.Crm;
using Microsoft.Extensions.DependencyInjection;
using Polly.CircuitBreaker;

namespace CrmIntegrationService.Tests;

public class CrmResiliencePipelineTests
{
    private static readonly CrmLeadPayload Payload = new(
        Guid.Parse("5b8f2f0e-8f6a-4c1e-9a3e-0d9c3a0f7b21"), "ada@example.com", "Ada", "starter-checklist",
        new DateTimeOffset(2026, 5, 4, 9, 0, 0, TimeSpan.Zero));

    private static ICrmClient Build(StubHttpHandler handler)
    {
        var services = new ServiceCollection();
        services.AddOptions<CrmOptions>().Configure(o =>
        {
            o.WebhookUrl = "https://crm.example.test/hooks/leads";
            o.Resilience = new CrmResilienceOptions
            {
                MaxRetryAttempts = 3,
                RetryDelay = TimeSpan.FromMilliseconds(1),
                AttemptTimeout = TimeSpan.FromSeconds(5),
                TotalTimeout = TimeSpan.FromSeconds(20),
                FailureRatio = 0.5,
                MinimumThroughput = 4,
                SamplingDuration = TimeSpan.FromSeconds(30),
                BreakDuration = TimeSpan.FromSeconds(30),
            };
        });
        services.AddCrmClient().ConfigurePrimaryHttpMessageHandler(() => handler);
        return services.BuildServiceProvider().GetRequiredService<ICrmClient>();
    }

    [Fact]
    public async Task Transient_503s_are_retried_until_success_with_the_same_idempotency_key()
    {
        var responses = new Queue<HttpStatusCode>([HttpStatusCode.ServiceUnavailable, HttpStatusCode.ServiceUnavailable, HttpStatusCode.OK]);
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(responses.Dequeue()));

        await Build(handler).SendLeadAsync(Payload, CancellationToken.None);

        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Equal(new Uri("https://crm.example.test/hooks/leads"), r.Uri);
            Assert.Equal("5b8f2f0e8f6a4c1e9a3e0d9c3a0f7b21", r.Headers["Idempotency-Key"]);
            Assert.Contains("\"email\":\"ada@example.com\"", r.Body);
        });
    }

    [Fact]
    public async Task Circuit_opens_after_repeated_failures_and_stops_calling_the_crm()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable));
        var client = Build(handler);

        await Assert.ThrowsAnyAsync<Exception>(() => client.SendLeadAsync(Payload, CancellationToken.None));
        var callsWhenOpened = handler.Requests.Count;

        await Assert.ThrowsAsync<BrokenCircuitException>(() => client.SendLeadAsync(Payload, CancellationToken.None));
        Assert.Equal(callsWhenOpened, handler.Requests.Count);

        // 1 attempt + 3 retries = 4 sampled failures = MinimumThroughput, which trips the breaker.
        Assert.Equal(4, callsWhenOpened);
    }

    [Fact]
    public async Task Non_transient_errors_are_not_retried()
    {
        var handler = new StubHttpHandler(_ => new HttpResponseMessage(HttpStatusCode.BadRequest));

        await Assert.ThrowsAsync<HttpRequestException>(() => Build(handler).SendLeadAsync(Payload, CancellationToken.None));
        Assert.Single(handler.Requests);
    }
}
