using System.Text.Json.Serialization;

public class GitHubRepositoriesResponse
{
    [JsonPropertyName("total_count")]
    public long total_count {get; set;}
    [JsonPropertyName("repositories")]
    public List<GitHubRepository> Repositories { get; set;} = new();
}