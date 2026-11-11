using System.Net;
using System.Text.Json;

public sealed class ScanHandler
{
    private readonly GitHubUserAuth _githubAuth;
    private readonly ScanApiClient _scanClient;
    private readonly ITokenStore _store;
    private readonly TextReader _input;
    private readonly TextWriter _output;

    public ScanHandler(GitHubUserAuth githubAuth, ScanApiClient scanClient, ITokenStore store,
        TextReader? input = null, TextWriter? output = null)
    {
        _githubAuth = githubAuth;
        _scanClient = scanClient;
        _store = store;
        _input = input ?? Console.In;
        _output = output ?? Console.Out;
    }

    public async Task<int> HandleAsync(bool all, string? repo, CancellationToken cancellationToken = default)
    {
        bool hasRepo = !string.IsNullOrWhiteSpace(repo);
        if (all == hasRepo)
        {
            _output.WriteLine("Specify exactly one: --all or --repo.");
            return 1;
        }
        if (hasRepo)
        {
            _output.WriteLine("scan --repo is not implemented yet.");
            return 1;
        }
        string? token = _store.GetToken();
        if (string.IsNullOrWhiteSpace(token))
        {
            _output.WriteLine("Please run auth login first!");
            return 1;
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cts.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            _output.WriteLine("Listing all available VS installations...");
            var installations = await _githubAuth.GetInstallationsAsync(token, cts.Token);
            if (installations.Count == 0)
            {
                _output.WriteLine("No available installations.");
                return 1;
            }
            for (int i = 0; i < installations.Count; i++)
                _output.WriteLine($"{i + 1}: {installations[i].accountLogin}");
            _output.WriteLine($"Select installation: [1-{installations.Count}]");
            string? input = await _input.ReadLineAsync(cts.Token);
            if (!int.TryParse(input, out int selection) || selection < 1 || selection > installations.Count)
            {
                _output.WriteLine($"Invalid installation selection, must be in range 1-{installations.Count}");
                return 1;
            }
            _output.WriteLine("Requesting scan for all authorized repositories; submission may wait up to 60 seconds for GitHub or queue capacity...");
            var result = await _scanClient.RequestAllAsync(installations[selection - 1].installationId, token, cts.Token);
            _output.WriteLine($"Scan Jobs queued: {result.jobIds.Count}");
            bool submissionFailed = result.SubmissionError is not null || result.NotSubmitted.Count > 0;
            if (result.SubmissionError is not null)
                _output.WriteLine($"Submission incomplete: {result.SubmissionError}");
            foreach (var entry in result.NotSubmitted)
                _output.WriteLine($"Not submitted: {entry.Key}: {entry.Value}");
            if (result.jobIds.Count == 0)
                return submissionFailed ? 1 : 0;
            _output.WriteLine("Polling for results...");
            FindingsResponse? findings;
            while ((findings = await _scanClient.GetFindingsAsync(result.jobIds, token, cts.Token)) is null)
                await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);

            int succeeded = result.jobIds.Count - findings.Failed.Count;
            _output.WriteLine($"Scans completed:{succeeded}, {findings.Failed.Count} failed");
            _output.WriteLine($"Potential secrets detected: {findings.Findings.Count}");
            foreach (var finding in findings.Findings)
                _output.WriteLine($"Found at {finding.FilePath}, on line {finding.LineNumber}, possibly: {finding.Provider}, {finding.CredentialType}");
            return !submissionFailed && findings.Failed.Count == 0 ? 0 : 1;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _output.WriteLine("Scan cancelled");
            return 103;
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("Scan request timed out.");
            return 1;
        }
        catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
        {
            _output.WriteLine("Previous session expired. Please run auth login again");
            return 1;
        }
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or JsonException or InvalidDataException)
        {
            _output.WriteLine($"Scan failed: {ex.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }
}
