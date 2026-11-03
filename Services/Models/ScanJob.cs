
using Octokit;

public record ScanJob(
    Guid JobId,
    long InstallationId,
    string CommitSha,
    string Name,
    string Login,
    DateTime CreatedAt,
    DateTime? FinishedAt = null
);