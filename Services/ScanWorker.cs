using Octokit;

//ScanWorker - Background Service implementation with scoped DI for ScanService
//Dequeues ScanJobs

public sealed class ScanWorker: BackgroundService
{
    private readonly ScanJobQueueSevice _queue;
    private readonly IServiceScopeFactory _scope;

public ScanWorker(ScanJobQueueSevice queue, IServiceScopeFactory scope)
{
    _queue = queue;
    _scope = scope;
}

    protected override async Task ExecuteAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var scanJob = await _queue.DequeueScanJobAsync(cancellationToken);
                long installationId = scanJob.InstallationId;
                string commitSha = scanJob.CommitSha;
                Repository repository = scanJob.Repository;

                using var scope = _scope.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<ScanService>();

                await service.RunScan(installationId, repository, commitSha);
            }
            catch (OperationCanceledException)
                when (cancellationToken.IsCancellationRequested)
            {
                break;
            }
            catch (RateLimitExceededException ex)
            {
                Console.WriteLine($"GitHub rate limit exceeded, status: {(int)ex.StatusCode}");
                Console.WriteLine($"Rate limit resets at: {ex.Reset}");
            }
            catch (ApiException ex)
            {
                Console.WriteLine($"GitHub API returned: {(int)ex.StatusCode}");
                Console.WriteLine($"Error: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Unexpected Scan failure: {ex.Message}");
            }

                
        }
    } 
}   