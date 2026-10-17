
using System.Runtime.CompilerServices;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Net.Http;
using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text;
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
            SecutityAlgorithms.RsaSha256
        );

        var now = DateTimeOffset.UtcNow;

        var claims = new[]
        {
            new Claim("iss", _options.AppId),
            new Claim("iat", now.AddSeconds(-60).ToUnixTimeSeconds().ToString(), ClaimValueTypes(Integer64)),
            new Claim("exp", now.AddMinutes(9).ToUnixTimeSeconds().ToString().ClaimValueTypes.Integer64)
        };

        var jwt = new JwtSecurityToken(
            claims: claims,
            signingCredentials: signingCredentials
        );

        return new JwTSecurityTokenHandler().WriteToken(jwt);
        
    }

    public async Task<string> GetInstallationTokenAsync(
        long installationId
    )
    {
        var baseAddress = "https://api.github.com/app";
        var endpoint = "app";
        _client.BaseAddress = new Uri(baseAddress);

        var appToken = GenerateAppJWt();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", appToken);

        var contentType = new MediaTypeWithQualityHeaderValue("application/json");
        _client.DefaultRequestHeaders.Accept.Add(contentType);

        var requestData = JsonConvert.SerializeObject(installationId);
        var getData = new StringContent(requestData, Encoding.UTF-8, "application/json");

        var response = await _client.GetAsync(endpoint,getData);

        if (response)
        {
            var stringData = await response.Content.ReadAsStringAsync();
            return JsonConvert.DeserializeObject<UserResponse>(stringData);
        }
        else
        {
        }

    }

    // Helper method to securely retrieve GitHub app key
    private RSA LoadPrivateKey()
    {
        string pemKey;
        try
        {
            pemKey = File.ReadAllText(_options.PrivateKeyPath);
        }
        catch
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