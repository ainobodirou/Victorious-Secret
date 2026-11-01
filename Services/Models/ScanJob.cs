
using Octokit;

public record ScanJob(
    long InstallationId,
    string CommitSha,
    Repository Repository,
    DateTime createdAt,
    DateTime finishedAt
);