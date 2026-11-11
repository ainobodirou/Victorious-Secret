using Octokit.Webhooks;
using Octokit.Webhooks.AspNetCore;
using Microsoft.AspNetCore.Authentication;

var builder = WebApplication.CreateBuilder(args);
//Register App Configuration
builder.Services.Configure<GitHubAppOptions>(builder.Configuration.GetSection("GitHubApp"));
//Add simple memory cache for auth result caching
builder.Services.AddMemoryCache();
//Add GitHub checkRun service
builder.Services.AddSingleton<GitHubCheckRunService>();
//Add token provider
builder.Services.AddSingleton<GitHubTokenProvider>();
//Add Octokit client
builder.Services.AddSingleton<GitHubClientO>();
//Configure HttpClient for GitHubTokenProvider
builder.Services.AddHttpClient<GitHubTokenProvider>(
    client =>
    {
        client.BaseAddress = new Uri("https://api.github.com/");
        client.DefaultRequestHeaders.UserAgent.Add(
            new System.Net.Http.Headers.ProductInfoHeaderValue(
                "SecAudit",
                "1.0"
            )
        );
        client.DefaultRequestHeaders.Accept.Add(
            new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(
                "application/vnd.github+json"
            )
        );
    });


builder.Services
    .AddAuthentication("GitHub")
    .AddScheme<AuthenticationSchemeOptions, GitHubAuthHandler>(
        "GitHub",
        options => { });

builder.Services.AddAuthorization();
builder.Services.AddHttpClient();

builder.Services.AddControllers();
builder.Services.AddTransient<ScanService>(); //Main Scan Orchestrator for all inputs
builder.Services.AddTransient<SecretDetector>(); //Secret Detection handling and logic
builder.Services.AddTransient<SourcePreprocessor>(); //Sourcecode preprocessing
builder.Services.AddSingleton<ScanJobStore>(); //Local ScanJob Store
builder.Services.AddSingleton<ProviderClassifier>(); //Trie & regex-based provider classification
builder.Services.AddSingleton(
    new ScanJobQueueSevice(100) //Scan job queue
);
builder.Services.AddHostedService<ScanWorker>(); //Scan Worker background service
builder.Services.AddSingleton<WebhookEventProcessor, WebhookProcessor>(); //Push event processor

string webhookSecret = builder.Configuration["GitHubApp:WebhookSecret"] ?? 
    throw new InvalidOperationException("GitHubWebhook secret is missing");

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.MapGitHubWebhooks(
    path: "/api/github/webhooks",
    secret: webhookSecret
);

if (app.Environment.IsDevelopment())
{
    string owner = "ainobodirou";
    string name = "Victorious-Secret";


    app.MapGet("/debug/github-auth", (GitHubTokenProvider provider) =>
    {
    string token = provider.GetInstallationTokenAsync(173102585).Result;
    return Results.Ok(  new { Success = true});
    });

    app.MapGet("/debug/github-tree", async (GitHubClientO client) =>
    {
        long installationId = 173102585;
        var repositories = await client.GetAllRepos(installationId);
        var repository = repositories[11];
        string login = repository.Owner.Login;
        string name = repository.Name;

        
        string commitSha = await client.GetDefaultBranchCommit(installationId,repository);

        var tree = await client.GetTreeAsync(installationId, login, name, commitSha);

        return Results.Ok(new
        {
           Repository = repository.FullName,
           CommitSha = commitSha,
           FileCount = tree.Tree.Count,
           IsTruncated = tree.Truncated,
        }
        );
    }
    );

}
app.Run();



