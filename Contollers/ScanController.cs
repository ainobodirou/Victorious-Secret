using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Channels;
using System.Web;
using Microsoft.AspNetCore.Mvc;
using Octokit;

namespace ScanController;

[ApiController]
[Route("[controller]")]
public class ScanController: Controller
{
    private readonly GitHubClientO _client;
    private readonly ScanJobQueueSevice _queue;

    public ScanController(GitHubClientO client, ScanJobQueueSevice queue)
    {
        _client = client;
        _queue = queue;
    }

    // POST: api/scans/all
    [HttpPost("all")]
    public async Task<IActionResult> RequestScanForAllAsync(long installationId, CancellationToken cancellationToken)
    {
        int queued = 0;
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
            try
            {
                await _queue.QueueScanJobAsync(scanJob, cancellationToken);
            }
            catch (ChannelClosedException)
            {
                return StatusCode(503, $"Scan Queue channel closed, but queued {queued} jobs");
            }
            queued += 1;
        }
        return Accepted(new
        {
            Message = $"Scan Jobs queued: {queued}"
        });
    }

            // public async Task<IActionResult> RequestScanForRepoAsync(long installaitonId, string name, CancellationToken cancellationToken)
            // {
            //     Repository repository = await_client.GetRepoAsync(installaitonId, owner, name);
            // }

}
