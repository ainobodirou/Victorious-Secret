using System.Net.Http.Headers;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;
using System.Text.Encodings.Web;

public sealed class GitHubAuthHandler: AuthenticationHandler<AuthenticationSchemeOptions>
{
    private readonly GitHubClientO _client;
    public GitHubAuthHandler(
        IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        GitHubClientO client)
        : base(options, logger, encoder)
    {
        _client = client;
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        var request = Request.Headers.Authorization;
        if(!AuthenticationHeaderValue.TryParse(request, out var bearer)
        || !string.Equals(bearer.Scheme, "Bearer", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(bearer.Parameter))
        {
            return AuthenticateResult.NoResult();
        }
        string userToken = bearer.Parameter;
        var client = _client.CreateUserClient(userToken);
        try
        {
            var user = await client.User.Current();
            var claim = new Claim(
                ClaimTypes.NameIdentifier, user.Id.ToString()
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
        catch (Octokit.AuthorizationException)
        {
            return AuthenticateResult.Fail("Invalid GitHub token");
        }
        

    }
}

