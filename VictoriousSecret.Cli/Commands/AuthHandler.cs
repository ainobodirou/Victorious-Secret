using System.Text.Json;

public sealed class AuthHandler
{
    private readonly Func<string, GitHubUserAuth> _createGitHubAuth;
    private readonly ITokenStore _store;
    private readonly TextWriter _output;
    private readonly TextReader _input;
    private readonly string? _configPath;

    public AuthHandler(Func<string, GitHubUserAuth> createGitHubAuth, ITokenStore store,
        TextWriter? output = null, TextReader? input = null, string? configPath = null)
    {
        _createGitHubAuth = createGitHubAuth;
        _store = store;
        _output = output ?? Console.Out;
        _input = input ?? Console.In;
        _configPath = configPath;
    }

    public async Task<int> LoginAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cts.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            // attempt to retrieve stroed clientId, prompt on failure and store.
            var (clientId, configPath) = await LoadConfigAsync(cts.Token);
            var _githubAuth = _createGitHubAuth(clientId);
            var deviceResponse = await _githubAuth.RequestDeviceAuthAsync(cts.Token);
            _output.WriteLine($"Proceed on GitHub auth URL: {deviceResponse.verification_uri}");
            _output.WriteLine($"Paste Code: {deviceResponse.user_code}");
            string token = await _githubAuth.GetUserAuthTokenAsync(deviceResponse, cts.Token);
            var config = new CliConfig { GitHubClientId = clientId };

            Directory.CreateDirectory(Path.GetDirectoryName(configPath)!);
            await File.WriteAllTextAsync(
            configPath, JsonSerializer.Serialize(config), cts.Token);

            _store.SaveToken(token);   
            _output.WriteLine("Successfully authenticated!");
            return 0;
        }
        catch (OperationCanceledException) when (cts.IsCancellationRequested)
        {
            _output.WriteLine("Login cancelled");
            return 103;
        }
        catch (OperationCanceledException)
        {
            _output.WriteLine("Login request timed out.");
            return 1;
        }
        catch (Exception ex) when (ex is 
        HttpRequestException or
        TimeoutException or
        JsonException or
        InvalidDataException or
        UnauthorizedAccessException or
        IOException)
        {
            _output.WriteLine($"Login failed: {ex.Message}");
            return 1;
        }
        finally
        {
            Console.CancelKeyPress -= handler;
        }
    }

    public int Logout()
    {
        bool removed = _store.Remove();
        _output.WriteLine(removed ? "Logged out successfully" : "Logout failed");
        return removed ? 0 : 1;
    }

    public int Status()
    {
        _output.WriteLine("auth status is not implemented yet.");
        return 1;
    }

    private async Task<(string clientId, string ConfigPath)> LoadConfigAsync(CancellationToken ct)
    {
        var path = _configPath;
        if (path is null)
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrEmpty(appData))
                throw new IOException("Could not find the user's application data folder.");

            path = Path.Combine(appData, "VictoriousSecret", "config.json");
        }

        CliConfig config;

        if (File.Exists(path))
        {
            string json = await File.ReadAllTextAsync(path, ct);
            config = JsonSerializer.Deserialize<CliConfig>(json) ?? throw new JsonException("expceted a config object in config.json");
            if (!string.IsNullOrWhiteSpace(config.GitHubClientId))
            {
               return (config.GitHubClientId.Trim(), path);
            }
        }
         _output.Write("GitHub App Client ID: ");
         _output.Flush();

        var clientId = (await _input.ReadLineAsync(ct))?.Trim();
        if (string.IsNullOrWhiteSpace(clientId))
        throw new InvalidDataException("A GitHub App Client ID is required.");

        return (clientId, path);
        
    }
}
