// Scan Service - main security scan orchestrator.
// Runs scan on traversed tree blobs, obtains file content from client and requests findings from the secret detector.
using System.Text;
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
    // Allowed file extensions for reading. 
    // Later: move to config, together with traversal depth and file size limit.
    private static readonly HashSet<string> SupportedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".cs", ".py", ".js", ".ts",
        ".json", ".yaml", ".yml",
        ".env", ".txt", ".config"
    };

    public async Task<List<SecretFinding>> RunScan(long installationId, string login, string name, string commitSha, Guid jobId, CancellationToken ct)
    {

        List<SecretFinding> allFindings = new();

        ct.ThrowIfCancellationRequested();
        TreeResponse tree = await _client.GetTreeAsync(installationId, login, name, commitSha);
        ct.ThrowIfCancellationRequested();

        if (tree.Truncated)
            throw new InvalidOperationException("Git Tree is truncated: cannot complete scan");

        foreach (var item in tree.Tree)
        {
            ct.ThrowIfCancellationRequested();
            if (item.Type != TreeType.Blob)
                continue;
                //later: filter binary, oversized and generated files!
            string extension = Path.GetExtension(item.Path);

            if (!SupportedExtensions.Contains(extension))
                continue;

            if (item.Size > 1_000_000) // Set arbitrary number for now
                continue;

            string content;

            try
            {
                content = await _client.GetFileContentAsync(installationId, login, name, item.Sha);
            }
                catch (DecoderFallbackException)
            {
                //File isnt valid UTF-8, skip for now
                //Later: handle in reporting unscanned part of the repository
                continue;
            }
            SourceFile sourceFile = new SourceFile(
                item.Path,
                content,
                commitSha
            );

            ct.ThrowIfCancellationRequested();
            //Link the findings with the associated jobId
            allFindings.AddRange(
                _detector.Detect(sourceFile).Select(f => f with {JobId = jobId})
            );
        }
        ct.ThrowIfCancellationRequested();
        return allFindings;
    }
}
