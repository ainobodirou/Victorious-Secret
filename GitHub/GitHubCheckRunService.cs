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
        CheckRunUpdate updateCheck;
        string summary, 
        details, title;

        var client = await _client.CreateClientAsync(installaitonId);

        if (findings is not null)
        {
            if (findings.Count == 0)
            {
                summary = $"No secrets detected";
            }
            else
            {
                details = string.Join("\n", findings.Select(f =>
                $"- **{f.FilePath}:{f.LineNumber}** — " +
                $"Possible {f.CredentialType} ({f.Provider}). " +
                $"Detection: {f.DetectionMethod}"));
                summary = $"Discovered {findings.Count} possible secrets for {owner}/{repo}, details: {details}";  
            }
            title = "Secret scan completed!"; 
        }
        else
        {
            summary = $"Could not scan {owner}/{repo} ";
            title = "Secret scan failed";
        }
            
        updateCheck = new CheckRunUpdate()
        {
            Name = "secret-scan",
            Status = CheckStatus.Completed,
            Conclusion = conclusion,
            CompletedAt = DateTimeOffset.Now,
            Output = new NewCheckRunOutput(
            title,
            summary
        )};

        await client.Check.Run.Update(
            owner,
            repo,
            checkRunId,
            updateCheck
        );
    }
}
