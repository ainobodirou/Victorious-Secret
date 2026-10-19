using System.Net.Http.Headers;

public class GitHubClient
{

    private readonly HttpClient _client;
    private readonly GitHubTokenProvider _tokenProvider;

    public GitHubClient(HttpClient client, GitHubTokenProvider tokenProvider){
        _client = client;
        _tokenProvider = tokenProvider;

    }

    // we need following methods:
    // 1. Fetch all available repositories from installationId
    // 2. Fetch a specific repository from installationId
    // What is the repository object? 
    public async Task<GitHubRepositoriesResponse> GetReposAsync(long installationId)
    {
        var endpoint = $"/installation/repositories";
        var appToken =  await _tokenProvider.GetInstallationTokenAsync(installationId);


        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            endpoint
        );

        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appToken);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json")
        );
        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        // Deserialization 

        var reposResponse = await response.Content.ReadFromJsonAsync<GitHubRepositoriesResponse>();
        if (reposResponse is null)
        {
            throw new InvalidOperationException(
                "GitHub returned invalid repositories response"
            );
        }
        return reposResponse;
    }

    public async Task<GitHubRepository> GetRepoAsync(long installationId, string owner, string repo)
    {   

        var endpoint = $"repos/{Uri.EscapeDataString(owner)}/{Uri.EscapeDataString(repo)}";
        var appToken =  _tokenProvider.GetInstallationTokenAsync(installationId).Result;

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            endpoint
        );

        

    }

    
}
