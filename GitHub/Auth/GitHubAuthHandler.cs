using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;
using Microsoft.Extensions.Caching.Memory;
using Octokit;
using System.Text;
using System.Runtime.Intrinsics.Arm;
using System.Security.Cryptography;

public sealed class GitHubAuthHandler: AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly GitHubClientO _client;
    private readonly IMemoryCache _cache;
    public GitHubAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        GitHubClientO client,
        IMemoryCache cache)
        : base(options, logger, encoder)
    {
        _client = client;
        _cache = cache;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var request = Request.Headers.Authorization;
        if(!AuthenticationHeaderValue.TryParse(request, out var bearer)
        || !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(bearer.Parameter))
        {
            return AuthenticateResult.NoResult();
        }
        string user_token = bearer.Parameter;
        string cacheKey = "git-auth:" + 
               Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(user_token)));

        bool cacheHit = _cache.TryGetValue(cacheKey, out long userId);
        Logger.LogInformation("GitHub auth cache hit: {CacheHit}", cacheHit);
        
        if(!cacheHit)
        {
            try
            {
                var client = _client.CreateUserClient(user_token);
                var user = await client.User.Current();

                userId = user.Id;
                _cache.Set(cacheKey, userId, TimeSpan.FromSeconds(60));
            }
            catch (Octokit.AuthorizationException)
            {
                return AuthenticateResult.Fail("Invalid GitHub token");
            }
        }
        var claim = new Claim(
            ClaimTypes.NameIdentifier, userId.ToString()
        );
        var identity = new ClaimsIdentity(
            [claim],
            Scheme.Name
        );
        var principal = new ClaimsPrincipal(
            identity
        );
        var ticket = new AuthenticationTicket(principal,Scheme.Name);
        return AuthenticateResult.Success(ticket);

     }


}


