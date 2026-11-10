using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Octokit;

namespace ScanController;

[ApiController]
[Authorize]
[Route("api/scan")]
public class ScanController: Controller
{
    private readonly GitHubClientO _client;
    private readonly ScanJobQueueSevice _queue;
    private readonly ScanJobStore _store;

    public ScanController(GitHubClientO client, ScanJobQueueSevice queue, ScanJobStore store)
    {
        _client = client;
        _queue = queue;
        _store = store;
    }

    // POST: api/scans/all
    [HttpPost("all")]
    public async Task<IActionResult> RequestScanForAllAsync(long installationId, CancellationToken cancellationToken)
    {
        int queued = 0;
        List<Guid> scanJobs = [];
        IReadOnlyList<Repository> repositories = await _client.GetAllRepos(installationId);
        foreach (Repository repo in repositories)
        {
            string commitSha;
            try
            {
                commitSha = await _client.GetDefaultBranchCommit(installationId, repo);
            }
            catch (NotFoundException)
            {
                //No accessible repository, possibly empy - Skip.
                continue;
            }
            ScanJob scanJob = new ScanJob(
                Guid.NewGuid(),
                installationId,
                commitSha,
                repo.Name,
                repo.Owner.Login,
                DateTime.Now
            );

            _store.Add(scanJob);
            try
            {
                await _queue.QueueScanJobAsync(scanJob, cancellationToken);
                scanJobs.Add(scanJob.JobId);
                queued +=1;

            }
            catch (ChannelClosedException)
            {
                _store.UpdateStatus(scanJob.JobId, ScanStatus.Failed);
                return StatusCode(503, $"Scan Queue channel closed, but queued {scanJobs.Count} jobs");
            }
        }
        return Accepted(new
        {
            Message = $"Scan Jobs queued: {scanJobs.Count}",
            scanJobs

        });
    }
    [HttpPost("findings")]
    public IActionResult GetFindings([FromBody] List<Guid> jobIds)
    {
        List<SecretFinding> findings = [];
        Dictionary<Guid ,string> failed = new();
        bool inProgress = false;
        foreach(Guid jobId in jobIds)
        {
            ScanJob? scanJob = _store.Get(jobId);
            if(scanJob is null)
            {
                return NotFound($"Job not found: {scanJob}");
            }
            if(scanJob.Status == ScanStatus.Failed)
            {
                failed.TryAdd(scanJob.JobId, scanJob.Name);
                continue;
            }
            if(scanJob.Status != ScanStatus.Completed)
            {
                inProgress = true;
            }
            findings.AddRange(scanJob.Findings);
        }
        if (inProgress)
        {
            return Accepted("Scans still in proces...");
        }
        return Ok(new
        {
            findings,
            failed,
        });
        }
}
