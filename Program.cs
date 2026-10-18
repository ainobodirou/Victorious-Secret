namespace SecAudit;

class Program
{
    static void Main(string[] args)
    {
        Console.WriteLine("Hello, World!");
    }
    builder.Services.AddHttpClient<GitHubTokenProvider>(client =>
{
    client.BaseAddress = new Uri("https://api.github.com/");
    client.DefaultRequestHeaders.UserAgent.ParseAdd("GitHubSecurityAuditor");
});
}
