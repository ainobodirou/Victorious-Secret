
public record ScanJob(
    long installationId,
    string name,
    string commitSha,
    DateTime createdAt,
    DateTime finishedAt
);