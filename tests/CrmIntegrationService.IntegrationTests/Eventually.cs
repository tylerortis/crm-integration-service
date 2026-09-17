namespace CrmIntegrationService.IntegrationTests;

internal static class Eventually
{
    /// <summary>Polls until <paramref name="probe"/> returns a value, for asserting on background work.</summary>
    public static async Task<T> GetAsync<T>(Func<Task<T?>> probe, TimeSpan? timeout = null)
        where T : class
    {
        var deadline = DateTime.UtcNow + (timeout ?? TimeSpan.FromSeconds(10));
        while (true)
        {
            if (await probe() is { } value)
            {
                return value;
            }

            if (DateTime.UtcNow > deadline)
            {
                throw new TimeoutException("Condition was not met before the timeout.");
            }

            await Task.Delay(50);
        }
    }
}
