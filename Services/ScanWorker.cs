using Octokit;

public sealed class ScanWorker : BackgroundService
{
    private readonly ScanJobQueueSevice _queue;
    private readonly IServiceScopeFactory _scope;
    private readonly ScanJobStore _store;
    private readonly GitHubCheckRunService _checkrun;

    public ScanWorker(
        ScanJobQueueSevice queue,
        IServiceScopeFactory scope,
        ScanJobStore store,
        GitHubCheckRunService checkrun)
    {
        _queue = queue;
        _scope = scope;
        _store = store;
        _checkrun = checkrun;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            ScanJob job;
            try
            {
                job = await _queue.DequeueScanJobAsync(ct);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                break;
            }
            try
            {
                await ProcessJobAsync(job);
            }
            catch (OperationCanceledException)
                when (ct.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _store.UpdateStatus(job.JobId, ScanStatus.Failed);
                Console.Error.WriteLine($"Unexpected worker error for {job.JobId}: {ex}");
            }
        }
    }

    private async Task ProcessJobAsync(ScanJob job)
    {
        if (!_store.UpdateStatus(job.JobId, ScanStatus.Running))
        {
            throw new InvalidOperationException($"Job {job.JobId} not found in store.");
        }
        long? checkRunId = null;
        if (job.IsWebhook)
        {
            try
            {
            checkRunId = await _checkrun.CreateCheckRunAsync(
                job.InstallationId,
                job.Login,
                job.Name,
                job.CommitSha
            );
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not create Check Run: {ex.Message}");
        } 
        }
        List<SecretFinding>? findings = null;
        try
        {
            using var scope = _scope.CreateScope();

            var service = scope.ServiceProvider
                .GetRequiredService<ScanService>();

            var results = await service.RunScan(
                job.InstallationId,
                job.Login,
                job.Name,
                job.CommitSha,
                job.JobId
            );

            if (!_store.Completion(job.JobId, results))
            {
                throw new InvalidOperationException($"Could not store results for {job.JobId}");
            }
            findings = results;
        }
        catch (Exception ex)
        {
            _store.UpdateStatus(job.JobId, ScanStatus.Failed);
            Console.Error.WriteLine($"Scan failed for {job.Login}/{job.Name}: {ex}");
        }
        if (checkRunId.HasValue)
        {
            try
            {
                CheckConclusion conclusion = findings is null
                    ? CheckConclusion.Failure
                    : findings.Count == 0
                        ? CheckConclusion.Success
                        : CheckConclusion.Failure;

                await _checkrun.UpdateCheckRunAsync(
                    job.InstallationId,
                    job.Login,
                    job.Name,
                    checkRunId.Value,
                    conclusion,
                    findings
                );
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"Could not update Check Run: {ex.Message}");
            }
        }
        if (findings is not null)
        {
            Console.WriteLine($"Scan completed for {job.Login}/{job.Name}. Findings: {findings.Count}");
        }
    }
}