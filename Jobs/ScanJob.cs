
using Octokit;
public enum ScanStatus
{
    Queued,
    Running,
    Completed,
    Failed
}public record ScanJob(
    Guid JobId,
    long InstallationId,
    string CommitSha,
    string Name,
    string Login,
    DateTime CreatedAt
)
{
    public ScanStatus Status {get; set;} = new();
    public List<SecretFinding> Findings {get; set;} = new();
    public bool IsWebhook {get; init;} = false;
}
