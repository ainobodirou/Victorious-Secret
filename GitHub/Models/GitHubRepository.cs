

using System.Text.Json.Serialization;

public class GitHubRepository
{
    [JsonPropertyName("id")]
    public long id { get; set;}
    [JsonPropertyName("name")]
    public string Name {get; set;} = string.Empty;

    [JsonPropertyName("full_name")]
    public string Full_name { get; set; } = string.Empty;

    [JsonPropertyName("default_branch")]
    public string Default_branch { get; set;} = string.Empty;

    [JsonPropertyName("owner")]
    public GitHubOwner Owner { get; set;} = new();

}

public class GitHubOwner
{
    [JsonPropertyName("login")]
    public string Login {get; set;} = string.Empty;
}