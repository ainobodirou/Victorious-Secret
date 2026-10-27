using Octokit;
using System.Data.SqlTypes;
using System.Runtime.Intrinsics.Arm;
using System.Text;
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
    //List all repositories available for the supplied installationId
    public async Task<IReadOnlyList<Repository>> GetAllRepos(long installationId)
    {
        var client = await CreateClientAsync(installationId);
        var response = await client.GitHubApps.Installation.GetAllRepositoriesForCurrent();

        return response.Repositories;
    }
    //Get a specific repository provided insallation id, owner and name.
    public async Task<Repository> GetRepoAsync(long installationId, string owner, string repo)
    {
        var client = await CreateClientAsync(installationId);
        return await client.Repository.Get(owner, repo);
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
    public async Task<TreeResponse> GetTreeAync(
        long installationId, Repository repository, string commitSha
    )
    {
        var client = await CreateClientAsync(installationId);

        TreeResponse tree = await client.Git.Tree.GetRecursive(
            repository.Owner.Login,
            repository.Name,
            commitSha
        );

        return tree;
    }
    public async Task<string> GetFileContentAsync(long installaitonId, Repository repository, string blobSha)
    {
        var client = await CreateClientAsync(installaitonId);
        // retrieve and decode files 
        Blob blob = await client.Git.Blob.Get(
            repository.Owner.Login,
            repository.Name,
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
