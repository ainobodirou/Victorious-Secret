using System.CommandLine;
using System.Net;
using System.Threading;

public class VSCli{
private const string clientId = "Iv23liWUWZclYZ27k5CA";
public static async Task<int> Main(string [] args)
    {

        using HttpClient httpClient = new HttpClient();
        GitHubUserAuth githubAuth = new GitHubUserAuth(
            clientId, httpClient
        );
        ScanApiClient scanClient = new ScanApiClient(
            httpClient);
        TokenStore store = new TokenStore();
        string? token = store.GetToken();

        
        Argument<string> repository = new("repository");
        RootCommand root = new("Victorious Secret CLI");
        Command scan = new("scan", "Scan repositories");
        Command auth = new("auth","Authorize for VS");
        Command login = new("login", "Authenticate through GitHub Device Flow");
        Command status = new("status", "Check session status");
        Command logout = new("logout", "Close session");

        Option<bool> allOption = new("--all")
        {
            Description = "Scan all acessible repositories"
        };
        Option<string?> repoOption = new("--repo")
        {
            Description = "Repository in owner/name format"
        };

        scan.Options.Add(allOption);
        scan.Options.Add(repoOption);
     
        login.SetAction(async parseResult =>
        {
            return await HandleLoginAsync(githubAuth, store);
        });

        logout.SetAction( _ =>
        {
            HandleLogout(store);
        }
        );

        scan.SetAction(async parseResult =>
        {
            bool all = parseResult.GetValue(allOption);
            string? repo = parseResult.GetValue(repoOption);
            return await HandleScanAsync(all, repo, scanClient, store, githubAuth);
        });

        auth.Subcommands.Add(logout);
        auth.Subcommands.Add(status);
        auth.Subcommands.Add(login);
        root.Subcommands.Add(auth);
        root.Subcommands.Add(scan);


        return await root.Parse(args).InvokeAsync();
    }

    private static async Task<int> HandleScanAsync(
    bool all,
    string? repo,
    ScanApiClient scanClient,
    TokenStore store,
    GitHubUserAuth githubAuth)
    {
        bool hasRepo = !string.IsNullOrWhiteSpace(repo);
        if (all == hasRepo)
        {
            Console.Error.WriteLine("Specify exactly one: --all or --repo.");
            return 1;
        }
        if (all)
        {   
            using var cts = new CancellationTokenSource();
            ConsoleCancelEventHandler handler = (_, e) =>
            {
                e.Cancel = true;
                cts.Cancel();
            };
            CancellationToken ct = new CancellationToken();
            string? access_token = store.GetToken();
            if (string.IsNullOrEmpty(access_token))
            {
                Console.WriteLine("Please run auth login first!");
                return 1;
            }
            try
            {
                Console.WriteLine("Listing all available VS installations...");
                List<(long installationId,string AccountLogin)> installations = await githubAuth.GetInstallationsAsync(access_token);

                if (installations.Count == 0){
                    Console.WriteLine("No available installations.");
                    return 1;
                }
                for(int i = 0; i < installations.Count; i++)
                {
                    Console.WriteLine($"{i+1}: {installations[i].AccountLogin}");
                }

                Console.WriteLine($"Select installation: [1-{installations.Count}]");
                string input = Console.ReadLine();
                if (!int.TryParse(input, out int selection)
                || selection < 1 || selection > installations.Count)
                {
                Console.WriteLine($"Invalid installation selection, must be in range 1-{installations.Count}");
                return 1;
                }

                long installationId = installations[selection-1].installationId;
                Console.WriteLine("Requesting scan for all authorized repositories...");
                Console.CancelKeyPress += handler;
                ScanRequestResult result = await scanClient.RequestAllAsync(installationId, access_token, cts.Token);
                Console.WriteLine($"Scan Jobs queued: {result.jobIds.Count}");
                Console.WriteLine($"Polling for results...");
                FindingsResponse? findings = null;
                while(findings is null)
                {
                    findings = await scanClient.GetFindingsAsync(result.jobIds, access_token, cts.Token);
                    if(findings is null)
                    {
                        await Task.Delay(TimeSpan.FromSeconds(2), cts.Token);
                    }
                }
                int succeeded = result.jobIds.Count - findings.Failed.Count;
                Console.WriteLine($"Scans completed:{succeeded}, {findings.Failed.Count} failed");
                Console.WriteLine($"Potential secrets detected: {findings.Findings.Count}");
                foreach(SecretFindingDTO f in findings.Findings)
                {
                    Console.WriteLine($"Found at {f.FilePath}, on line{f.LineNumber}, possibly: {f.Provider}, {f.CredentialType}");
                }
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                Console.WriteLine($"Scan cancelled");
                return 103;
            }
            catch (HttpRequestException ex) when (ex.StatusCode == HttpStatusCode.Unauthorized)
            {
                Console.WriteLine($"Previous session expired. Please run --auth login again");
            }
            finally
            {
                Console.CancelKeyPress -= handler;
            }
            return 0;
        }
        return 1;
    }
        


private static async Task<int> HandleLoginAsync(
    GitHubUserAuth githubAuth,
    TokenStore store)
    {
    DeviceAuthResponse deviceResponse = await githubAuth.RequestDeviceAuthAsync();
   
    Console.WriteLine($"Proceed on GitHub auth URL: {deviceResponse.verification_uri}");
    Console.WriteLine($"Paste Code: {deviceResponse.user_code}");

    string access_token = await githubAuth.GetUserAuthTokenAsync(deviceResponse);
    store.SaveToken(access_token);

    Console.WriteLine($"Succesfully Authenticated!");
    return 0;
    }

private static int HandleLogout(ITokenStore store)
    {
        bool removed = store.Remove();
        Console.WriteLine(removed? "Logged out succesfully" : "Logout failed");
        return 0;
    }

}
