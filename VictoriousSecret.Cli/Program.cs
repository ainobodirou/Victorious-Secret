using System.CommandLine;

public class VSCli
{
    public static async Task<int> Main(string[] args)
    {
        var apiUrl = Environment.GetEnvironmentVariable("VS_API_URL") ?? "http://localhost:5000";
        var clientId = Environment.GetEnvironmentVariable("VS_GITHUB_CLIENT_ID") ?? "Iv23liWUWZclYZ27k5CA";
        if (!Uri.TryCreate(apiUrl, UriKind.Absolute, out var apiUri) 
        || (apiUri.Scheme != Uri.UriSchemeHttp && apiUri.Scheme != Uri.UriSchemeHttps))
        {
        Console.Error.WriteLine("VS_API_URL must be an absolute HTTP or HTTPS URL.");
        return 1;
        }
        using var githubHttpClient = new HttpClient();
        using var apiHttpClient = new HttpClient
        {
        BaseAddress = new Uri(apiUri.AbsoluteUri.TrimEnd('/') + "/")
        };
        var githubAuth = new GitHubUserAuth(clientId, githubHttpClient);
        var scanClient = new ScanApiClient(apiHttpClient);
        var store = new TokenStore();
        var authHandler = new AuthHandler(githubAuth, store);
        var scanHandler = new ScanHandler(githubAuth, scanClient, store);
        return await CreateRootCommand(authHandler, scanHandler).Parse(args).InvokeAsync();
    }

    public static RootCommand CreateRootCommand(AuthHandler authHandler, ScanHandler scanHandler)
    {
        RootCommand root = new("Victorious Secret CLI");
        Command scan = new("scan", "Scan repositories");
        Command auth = new("auth", "Authorize for VS");
        Command login = new("login", "Authenticate through GitHub Device Flow");
        Command status = new("status", "Check session status");
        Command logout = new("logout", "Close session");
        Option<bool> allOption = new("--all") { Description = "Scan all acessible repositories" };
        Option<string?> repoOption = new("--repo") { Description = "Repository in owner/name format" };

        scan.Options.Add(allOption);
        scan.Options.Add(repoOption);
        login.SetAction(_ => authHandler.LoginAsync());
        logout.SetAction(_ => authHandler.Logout());
        status.SetAction(_ => authHandler.Status());
        scan.SetAction(result => scanHandler.HandleAsync(result.GetValue(allOption), result.GetValue(repoOption)));
        auth.Subcommands.Add(logout);
        auth.Subcommands.Add(status);
        auth.Subcommands.Add(login);
        root.Subcommands.Add(auth);
        root.Subcommands.Add(scan);
        return root;
    }
}
