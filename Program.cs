using Octokit;
using Microsoft.Extensions.DependencyInjection;

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
    app.MapGet("/debug/github-auth", (GitHubTokenProvider provider) =>
    {
    string token = provider.GetInstallationTokenAsync(173102585).Result;
    return Results.Ok(  new { Success = true});
    });

    app.MapGet("/debug/github-repos", (GitHubClientO client) =>
    {
        var repositories = client.GetAllRepos(173102585).Result;
        return Results.Ok( new{
            Count = repositories.Count
        });
    }
    );};

app.Run();



