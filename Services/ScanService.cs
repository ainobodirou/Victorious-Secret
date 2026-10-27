

using Octokit;

public class ScanService
{
    private readonly GitHubClientO _client;
    private readonly SecretDetector _detector;
    public ScanService(GitHubClientO client, SecretDetector detector)
    {
        _client = client;
        _detector = detector;

    }

    public async Task<string> RunScan(long installationId, Repository repository, string commitSha)
    {

        TreeResponse tree = await _client.GetTreeAync(installationId, repository, commitSha);

        foreach (var item in tree.Tree)
        {
            if (item.Type != TreeType.Blob)
            {
                continue;
                //later: filter binary, oversized and generated files!
            }
            string blobSha = item.Sha;

            string content = await _client.GetFileContentAsync(installationId, repository, blobSha);
            SourceFile sourceFile = new SourceFile(
                item.Path,
                content,
                commitSha
            );

            var findings = _detector.Detect(sourceFile);
            return "";
        }
        return "";
    }

        
}
