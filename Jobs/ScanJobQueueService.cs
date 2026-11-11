using System.Threading.Channels;

public sealed class ScanJobQueueSevice
{
    private readonly Channel<ScanJob> _scanQueue;

    public ScanJobQueueSevice(int capacity)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(capacity),
                "Que capacity must be positive"
            );
        }
        BoundedChannelOptions options = new(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait
        };

        _scanQueue = Channel.CreateBounded<ScanJob>(options);
    }

    public async ValueTask QueueScanJobAsync(ScanJob scanJob, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(scanJob);

        await _scanQueue.Writer.WriteAsync(scanJob, ct);
    }

    public void Complete() => _scanQueue.Writer.TryComplete();

    public async ValueTask<ScanJob> DequeueScanJobAsync(CancellationToken cancellationToken)
{
    ScanJob scanJob = await _scanQueue.Reader.ReadAsync(cancellationToken);
    return scanJob;
}
}
