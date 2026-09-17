using System.Threading.Channels;

namespace CrmIntegrationService.Crm;

public interface IForwardSignal
{
    void Notify();

    /// <summary>Completes when notified (true) or when the timeout elapses (false).</summary>
    Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken);
}

/// <summary>Wakes the forwarding worker early; extra notifications while one is pending collapse into one.</summary>
public sealed class ForwardSignal : IForwardSignal
{
    private readonly Channel<bool> _channel = Channel.CreateBounded<bool>(
        new BoundedChannelOptions(1) { FullMode = BoundedChannelFullMode.DropWrite });

    public void Notify() => _channel.Writer.TryWrite(true);

    public async Task<bool> WaitAsync(TimeSpan timeout, CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        try
        {
            await _channel.Reader.ReadAsync(timeoutSource.Token);
            return true;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return false;
        }
    }
}
