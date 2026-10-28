

using System.Text;
using Octokit;
// Scan Service - main security scan orchestrator.
// Runs scan on traversed tree blobs, obtains file content from client and requests findings from the secret detector.
public class ScanService
{
    private readonly GitHubClientO _client;
    private readonly SecretDetector _detector;
    public ScanService(GitHubClientO client, SecretDetector detector)
    {
        _client = client;
        _detector = detector;

    }
    // Allowed file extensions for reading. 
    // Later: move to config, together with traversal depth and file size limit.
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".py", ".js", ".ts",
        ".json", ".yaml", ".yml",
        ".env", ".txt", ".config"
    };

    public async Task<List<SecretCandidate>> RunScan(long installationId, Repository repository, string commitSha)
    {
        List<SecretCandidate> allFindings = new();

        TreeResponse tree = await _client.GetTreeAsync(installationId, repository, commitSha);

        if (tree.Truncated)
        {
            throw new InvalidOperationException("Git Tree is truncated: cannot complete scan");
        }

        foreach (var item in tree.Tree)
        {
            if (item.Type != TreeType.Blob)
            {
                continue;
                //later: filter binary, oversized and generated files!
            }
            string extension = Path.GetExtension(item.Path);

            if (!SupportedExtensions.Contains(extension)){
                continue;
            }
            if (item.Size > 1_000_000) // Set arbitrary number for now
            {
                continue;
            }
            string content;
            try
            {
            content = await _client.GetFileContentAsync(installationId, repository, item.Sha);
            }
            catch (DecoderFallbackException)
            {
                // File isnt valid UTF-8, skip for now
                //Later: handle in reporting unscanned part of the repository
                continue;
            }
            SourceFile sourceFile = new SourceFile(
                item.Path,
                content,
                commitSha
            ); 

            var findings = _detector.Detect(sourceFile);
            allFindings.AddRange(findings);
        }
        return allFindings;
    }

        
}
