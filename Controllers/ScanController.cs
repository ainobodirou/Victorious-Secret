using System.Threading.Channels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net;
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
    private readonly TimeProvider _timeProvider;

    public ScanController(GitHubClientO client, ScanJobQueueSevice queue, ScanJobStore store, TimeProvider? timeProvider = null)
    {
        _client = client;
        _queue = queue;
        _store = store;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    // POST: api/scans/all
    [HttpPost("all")]
    public async Task<IActionResult> RequestScanForAllAsync(long installationId, CancellationToken ct)
    {
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(60), _timeProvider);
        using var submission = CancellationTokenSource.CreateLinkedTokenSource(ct, deadline.Token);
        List<Guid> scanJobs = [];
        Dictionary<string, string> notSubmitted = new();
        string? submissionError = null;
        IReadOnlyList<Repository> repositories = [];
        int index = 0;
        ScanJob? pendingJob = null;

        try
        {
            repositories = await WithRetryAsync(() => _client.GetAllRepos(installationId), submission.Token);
            for (; index < repositories.Count; index++)
            {
                submission.Token.ThrowIfCancellationRequested();
                Repository repo = repositories[index];
                string commitSha;
                try
                {
                    commitSha = await WithRetryAsync(
                        () => _client.GetDefaultBranchCommit(installationId, repo), submission.Token);
                }
                catch (NotFoundException)
                {
                    notSubmitted[$"{repo.Owner.Login}/{repo.Name}"] = "Repository or default branch is missing, empty, or inaccessible.";
                    continue;
                }
                catch (ApiException ex) when (ex.StatusCode == HttpStatusCode.Forbidden)
                {
                    notSubmitted[$"{repo.Owner.Login}/{repo.Name}"] = "Repository is inaccessible.";
                    continue;
                }

                submission.Token.ThrowIfCancellationRequested();
                pendingJob = new ScanJob(Guid.NewGuid(), installationId, commitSha,
                    repo.Name, repo.Owner.Login, DateTime.Now);
                _store.Add(pendingJob);
                await _queue.QueueScanJobAsync(pendingJob, submission.Token);
                scanJobs.Add(pendingJob.JobId);
                pendingJob = null;
            }
            submission.Token.ThrowIfCancellationRequested();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            if (pendingJob is not null)
                _store.UpdateStatus(pendingJob.JobId, ScanStatus.Failed);
            throw;
        }
        catch (OperationCanceledException) when (deadline.IsCancellationRequested)
        {
            submissionError = "Submission timed out after 60 seconds.";
        }
        catch (ChannelClosedException)
        {
            submissionError = "Scan queue is unavailable.";
        }
        catch (ApiException ex)
        {
            submissionError = $"GitHub rejected submission (HTTP {(int)ex.StatusCode}).";
        }
        catch (HttpRequestException ex)
        {
            submissionError = ex.StatusCode is { } status
                ? $"GitHub request failed (HTTP {(int)status})."
                : "GitHub request failed.";
        }

        if (submissionError is not null)
        {
            if (pendingJob is not null)
                _store.UpdateStatus(pendingJob.JobId, ScanStatus.Failed);
            for (; index < repositories.Count; index++)
            {
                var repo = repositories[index];
                notSubmitted[$"{repo.Owner.Login}/{repo.Name}"] = submissionError;
            }
        }
        var result = new
        {
            Message = $"Scan Jobs queued: {scanJobs.Count}",
            scanJobs,
            NotSubmitted = notSubmitted,
            SubmissionError = submissionError
        };
        return scanJobs.Count > 0 ? Accepted(result) : Ok(result);
    }

    private async Task<T> WithRetryAsync<T>(Func<Task<T>> operation, CancellationToken ct)
    {
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            TimeSpan retryDelay;
            try
            {
                return await operation().WaitAsync(ct);
            }
            catch (RateLimitExceededException ex)
            {
                retryDelay = TimeSpan.FromSeconds(Math.Clamp(
                    (ex.Reset - _timeProvider.GetUtcNow()).TotalSeconds, 1, 60));
            }
            catch (ApiException ex) when ((int)ex.StatusCode >= 500)
            {
                retryDelay = TimeSpan.FromSeconds(2);
            }
            catch (HttpRequestException ex) when (ex.StatusCode is null || (int)ex.StatusCode >= 500)
            {
                retryDelay = TimeSpan.FromSeconds(2);
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                retryDelay = TimeSpan.FromSeconds(2);
            }
            catch (TimeoutException)
            {
                retryDelay = TimeSpan.FromSeconds(2);
            }
            await Task.Delay(retryDelay, _timeProvider, ct);
        }
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
