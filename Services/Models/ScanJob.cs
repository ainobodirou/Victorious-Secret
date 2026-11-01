
using Octokit;

public record ScanJob(
    Guid jobId,
    long InstallationId,
    string CommitSha,
    Repository Repository,
    DateTime createdAt,
    DateTime? finishedAt = null
);