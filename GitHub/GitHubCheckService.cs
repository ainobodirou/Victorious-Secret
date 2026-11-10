using Octokit;

public class GitHubCheckRunService
{
    private readonly GitHubClientO _client;

    public GitHubCheckRunService(GitHubClientO client)
    {
        _client = client;
    }

    public async Task<long> CreateCheckRunAsync(long installationId, string owner, string repo, string commitSha)
    {
        var secretCheck = new NewCheckRun(
            "secret-scan",
            commitSha
        )
        {
        Status = CheckStatus.InProgress
        };

        var client = await _client.CreateClientAsync(installationId);
        var createdCheck = await client.Check.Run.Create(
            owner,
            repo,
            secretCheck
        );
        return createdCheck.Id;
    }

    public async Task UpdateCheckRunAsync(long installaitonId, string owner, string repo, long checkRunId, CheckConclusion conclusion, List<SecretFinding>? findings = null)
    {
        string summary;
        if (findings is null) summary = "Secret scan could not be completed.";
        else
        {
        string details = string.Join("/n", findings.Select(f =>
        $" Found at: {f.FilePath} - Line {f.LineNumber}: Possible {f.CredentialType} from {f.Provider}." +
        $"Detection method: {f.DetectionMethod}"
        ));
        summary = $"Potentia secrets detected: {findings.Count}\n\n{details}";    
        }
        var client = await _client.CreateClientAsync(installaitonId);
        var updateCheck = new CheckRunUpdate()
        {
        Name = "secret-scan",
        Status = CheckStatus.Completed,
        Conclusion = conclusion,
        CompletedAt = DateTimeOffset.Now,
        Output = new NewCheckRunOutput(
            title: "Secret scan completed",
            summary
        )
        };
        await client.Check.Run.Update(
            owner,
            repo,
            checkRunId,
            updateCheck
        );
    }
}
