using System.Text.Json;

public sealed class AuthHandler
{
    private readonly GitHubUserAuth _githubAuth;
    private readonly ITokenStore _store;
    private readonly TextWriter _output;

    public AuthHandler(GitHubUserAuth githubAuth, ITokenStore store, TextWriter? output = null)
    {
        _githubAuth = githubAuth;
        _store = store;
        _output = output ?? Console.Out;
    }

    public async Task<int> LoginAsync(CancellationToken cancellationToken = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        ConsoleCancelEventHandler handler = (_, e) => { e.Cancel = true; cts.Cancel(); };
        Console.CancelKeyPress += handler;
        try
        {
            var deviceResponse = await _githubAuth.RequestDeviceAuthAsync(cts.Token);
            _output.WriteLine($"Proceed on GitHub auth URL: {deviceResponse.verification_uri}");
            _output.WriteLine($"Paste Code: {deviceResponse.user_code}");
            string token = await _githubAuth.GetUserAuthTokenAsync(deviceResponse, cts.Token);
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
        catch (Exception ex) when (ex is HttpRequestException or TimeoutException or JsonException or InvalidDataException)
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
}
