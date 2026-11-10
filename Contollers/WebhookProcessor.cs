using Octokit.Webhooks;
using Octokit.Webhooks.Events;


public sealed class WebhookProcessor: WebhookEventProcessor
{
    private readonly ScanJobQueueSevice _queue;
    private readonly ScanJobStore _store;

    public WebhookProcessor(ScanJobQueueSevice queue, ScanJobStore store)
    {
        _queue = queue;
        _store = store;
        // Supply GitHubOptions later in case we'll need extra info from Github.

    }
    protected override async ValueTask ProcessPushWebhookAsync(WebhookHeaders headers, PushEvent pushEvent, CancellationToken cancellationToken = default)
    {
        if (pushEvent.Installation is null)
        {
            throw new Exception("Missing or incorrect InstallationId");
        }
        if (pushEvent.Deleted || pushEvent.Ref != $"refs/heads/{pushEvent.Repository.DefaultBranch}")
        {
            return;
        }
        ScanJob scanJob = new ScanJob(
            Guid.NewGuid(),
            pushEvent.Installation.Id,
            pushEvent.After,
            pushEvent.Repository.Name,
            pushEvent.Repository.Owner.Login,
            DateTime.UtcNow
        )
        {
            IsWebhook = true
        };
        _store.Add(scanJob);
        try
        {
            await _queue.QueueScanJobAsync(scanJob, cancellationToken);
        }
        catch
        {
            _store.UpdateStatus(scanJob.JobId, ScanStatus.Failed);
            throw;
        }
        }

}
