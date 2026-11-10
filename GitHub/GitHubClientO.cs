using Octokit;
using System.Collections.Concurrent;
using System.Data.SqlTypes;
using System.Runtime.Intrinsics.Arm;
using System.Text;
using System.Threading.Tasks;
using OctokitClient = Octokit.GitHubClient;

public class GitHubClientO
{
    private readonly GitHubTokenProvider _tokenProvider;
    public readonly ConcurrentDictionary<long, (string token, OctokitClient Client)> client_cache = new();

    public GitHubClientO(GitHubTokenProvider tokenProvider)
    {
        _tokenProvider = tokenProvider;
    }

    //Create Octokit client
    //v2: Encapsulate and replace with client factory instance through CI 
    public async Task<OctokitClient> CreateClientAsync(long installationId)
    {
        var check_token = await _tokenProvider.GetInstallationTokenAsync(installationId);
        if(client_cache.TryGetValue(installationId, out var cached) && cached.token == check_token)
        {
            return cached.Client;
        }
        var client = new OctokitClient(new ProductHeaderValue("VictoriousSecret"));
        client.Credentials = new Credentials(check_token);
        client_cache[installationId] = (check_token,client);
        return client;
    }
    public OctokitClient CreateUserClient(string userToken)
    {
        var client = new OctokitClient(new ProductHeaderValue("VictoriousSecret"));
        client.Credentials = new Credentials(userToken);
        return client;
    }
    
    //List all repositories available for the supplied installationId
    public async Task<IReadOnlyList<Repository>> GetAllRepos(long installationId)
    {   
        var client = await CreateClientAsync(installationId);
        var response = await client.GitHubApps.Installation.GetAllRepositoriesForCurrent();

        return response.Repositories;
    }
    //Get a specific repository provided insallation id, owner and name.
    public async Task<Repository> GetRepoAsync(long installationId, string owner, string name)
    {
        var client = await CreateClientAsync(installationId);
        return await client.Repository.Get(owner, name);
    }

    //Retrieve last commit on provided repositories last branch
    public async Task<string> GetDefaultBranchCommit(long installationId,Repository repository)
    {
        var client = await CreateClientAsync(installationId);

        var branch = await client.Repository.Branch.Get(
            repository.Owner.Login,
            repository.Name,
            repository.DefaultBranch
        );

        return branch.Commit.Sha;

    }
    // Recursively retrieve git tree for a specific repo
    public async Task<TreeResponse> GetTreeAsync(
        long installationId,string login, string name, string commitSha
    )
    {
        var client = await CreateClientAsync(installationId);

        TreeResponse tree = await client.Git.Tree.GetRecursive(
            login,
            name,
            commitSha
        );

        return tree;
    }
    public async Task<string> GetFileContentAsync(long installaitonId, string login, string name, string blobSha)
    {
        var client = await CreateClientAsync(installaitonId);
        // retrieve and decode files 
        Blob blob = await client.Git.Blob.Get(
            login,
            name,
            blobSha
        );

        if (blob.Encoding != EncodingType.Base64)
        {
            throw new NotSupportedException($"Unsupported blob encoding: {blob.Encoding.StringValue}");
        }
        byte[] bytes = Convert.FromBase64String(blob.Content);
        return new UTF8Encoding(
            encoderShouldEmitUTF8Identifier: false,
            throwOnInvalidBytes: true).GetString(bytes);
    }
    

    //



}
