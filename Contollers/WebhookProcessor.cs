using Octokit.Webhooks;
using Octokit.Webhooks.Events;


public sealed class WebhookProcessor: WebhookEventProcessor
{
    private readonly ScanJobQueueSevice _queue;
    private readonly GitHubAppOptions _options;

    public WebhookProcessor(ScanJobQueueSevice queue, GitHubAppOptions options)
    {
        _queue = queue;
        _options = options; // Supply GitHubOptions in case we'll need extra info from Github.

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
        );

        await _queue.QueueScanJobAsync(scanJob, cancellationToken);
    }
}