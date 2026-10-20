using Octokit;
using System.Collections.Generic;
using System.Net.Http.Headers;
using System.Threading.Tasks;
using OctokitClient = Octokit.GitHubClient;

public class GitHubClientO
{
    private readonly GitHubTokenProvider _tokenProvider;

    public GitHubClientO(GitHubTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    //Create Octokit client
    //v2: Encapsulate and replace with client factory instance through CI 

    public async Task<OctokitClient> CreateClientAsync(long installationId)
    {
        string token = await _tokenProvider.GetInstallationTokenAsync(installationId);
        var client = new OctokitClient(new Octokit.ProductHeaderValue("VictoriousSecret"));

        client.Credentials = new Credentials(token);
        return client;
    }

    public async Task<IReadOnlyList<Repository>> GetAllRepos(long installationId)
    {
        var client = await CreateClientAsync(installationId);
        var response = await client.GitHubApps.Installation.GetAllRepositoriesForCurrent();

        return response.Repositories;
    }

    public async Task<Repository> GetRepoAsync(long installationId, string owner, string repo)
    {
        var client = await CreateClientAsync(installationId);
        return await client.Repository.Get(owner, repo);
    }



}