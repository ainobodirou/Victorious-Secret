using Octokit;
using Microsoft.Extensions.DependencyInjection;
using Octokit.Caching;

var builder = WebApplication.CreateBuilder(args);



//Register App Configuration
builder.Services.Configure<GitHubAppOptions>(builder.Configuration.GetSection("GitHubApp"));

//Add token provider
builder.Services.AddTransient<GitHubTokenProvider>();

//Add Octokit client
builder.Services.AddTransient<GitHubClientO>();

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
builder.Services.AddControllers();


var app = builder.Build();

app.MapControllers();

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
        var repository = repositories[1];
        
        string commitSha = await client.GetDefaultBranchCommit(installationId,repository);

        var tree = await client.GetTreeAync(installationId, repository, commitSha);

        return Results.Ok(new
        {
           Repository = repository.FullName,
           CommitSha = commitSha,
           FileCount = tree.Tree.Count,
           IsTruncated = tree.Truncated,
        }
        );
    }
    );};


app.Run();



