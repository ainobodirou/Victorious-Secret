using System.Security.Claims;
using System.Security.Cryptography;
using System.IdentityModel.Tokens.Jwt;
using System.Text.Json;
using System.Net.Http.Headers;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

public class GitHubTokenProvider
{
    private readonly IOptions<GitHubAppOptions> _options;
    private readonly HttpClient _client;
    public GitHubTokenProvider(
        IOptions<GitHubAppOptions> appOptions,
        HttpClient appClient)
    {
        _options = appOptions;
        _client  = appClient;
    }
    public string GenerateAppJWt()
    {
        using RSA rsa = LoadPrivateKey();

        var securityKey = new RsaSecurityKey(rsa);

        var signingCredentials = new SigningCredentials(
            securityKey,
            SecurityAlgorithms.RsaSha256
        );

        var now = DateTimeOffset.UtcNow;

        var claims = new[]
        {
            new Claim("iss", _options.Value.AppId),
            new Claim("iat", now.AddSeconds(-60).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
            new Claim("exp", now.AddMinutes(9).ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
        };

        var jwt = new JwtSecurityToken(
            claims: claims,
            signingCredentials: signingCredentials
        );

        return new JwtSecurityTokenHandler().WriteToken(jwt);
        
    }

    public async Task<string> GetInstallationTokenAsync(
        long installationId
    )
    {
        var endpoint = $"app/installations/{installationId}/access_tokens";
        var appToken = GenerateAppJWt();

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            endpoint
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", appToken);
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));

        using var response = await _client.SendAsync(request);
        response.EnsureSuccessStatusCode();

        var stringData = await response.Content.ReadAsByteArrayAsync();
        var tokenResponse = JsonSerializer.Deserialize<InstallationTokenResponse>(stringData);

        if(String.IsNullOrWhiteSpace(tokenResponse?.Token))
        {
            throw new InvalidOperationException(
                "Failed to retrieve installation token"
            );
        }
        else
        {
            return tokenResponse.Token;
        }


    }

    // Helper method to securely retrieve GitHub app key
    private RSA LoadPrivateKey()
    {
        string pemKey;
        try
        {
            pemKey = File.ReadAllText(_options.Value.PrivateKeyPath);
        }
        catch (Exception ex)
        {
            
            throw new InvalidOperationException(
                "Failed to load GitHub App PK", ex
            );
        }
        RSA rsa = RSA.Create();
        try
        {
            rsa.ImportFromPem(pemKey);
            return rsa;
        }
        catch
        {
            rsa.Dispose();
            throw;
        }
    }
}
