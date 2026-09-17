using Microsoft.Extensions.Http.Resilience;
using Microsoft.Extensions.Options;
using Polly;

namespace CrmIntegrationService.Crm;

public static class CrmServiceCollectionExtensions
{
    public const string PipelineName = "crm";

    public static IHttpClientBuilder AddCrmClient(this IServiceCollection services)
    {
        var builder = services.AddHttpClient<ICrmClient, CrmClient>();

        // Order matters: the total timeout bounds everything; retries wrap the breaker so every
        // attempt is sampled; the per-attempt timeout is innermost.
        builder.AddResilienceHandler(PipelineName, (pipeline, context) =>
        {
            var o = context.ServiceProvider.GetRequiredService<IOptions<CrmOptions>>().Value.Resilience;

            pipeline
                .AddTimeout(o.TotalTimeout)
                .AddRetry(new HttpRetryStrategyOptions
                {
                    MaxRetryAttempts = o.MaxRetryAttempts,
                    Delay = o.RetryDelay,
                    BackoffType = DelayBackoffType.Exponential,
                    UseJitter = true,
                })
                .AddCircuitBreaker(new HttpCircuitBreakerStrategyOptions
                {
                    FailureRatio = o.FailureRatio,
                    MinimumThroughput = o.MinimumThroughput,
                    SamplingDuration = o.SamplingDuration,
                    BreakDuration = o.BreakDuration,
                })
                .AddTimeout(o.AttemptTimeout);
        });

        return builder;
    }
}
