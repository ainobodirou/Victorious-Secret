
using System.Collections.Concurrent;

public class ScanJobStore
{
    private readonly ConcurrentDictionary<Guid, ScanJob> _jobs = new();
    public void Add(ScanJob scanJob)
    {
        _jobs.TryAdd(scanJob.JobId, scanJob);
    }

    public bool UpdateStatus(Guid jobId, ScanStatus status)
    {
        if (_jobs.TryGetValue(jobId, out ScanJob? scanJob))
        {
            scanJob.Status = status;
            return true;
        }
        return false;
    }

    public ScanJob? Get(Guid jobId)
    {
        _jobs.TryGetValue(jobId, out ScanJob? scanJob);
        return scanJob;
    }

    public bool Completion(Guid jobId, List<SecretFinding> findings)
    {
        if(!_jobs.TryGetValue(jobId, out ScanJob? scanJob))
        {
            return false;
        }
        lock (scanJob)
        {
            scanJob.Findings = findings;
            scanJob.Status = ScanStatus.Completed;

        }
        return true;
    }

}