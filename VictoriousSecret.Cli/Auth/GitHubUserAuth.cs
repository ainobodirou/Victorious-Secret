using System.Net.Http.Headers;
using System.Text.Json;
using System.Linq;

public class GitHubUserAuth
{
    private readonly string _clientId;
    private readonly HttpClient _client;

    public GitHubUserAuth(string clientId, HttpClient client)
    {
        _clientId = clientId;
        _client = client;
    }

    //Initiate device auth flow - request device_code and user_access token verification
    public async Task<DeviceAuthResponse> RequestDeviceAuthAsync()
    {
        var endpoint = $"https://github.com/login/device/code?client_id={Uri.EscapeDataString(_clientId)}";
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            endpoint
        );
        request.Headers.Accept.Add(
            new MediaTypeWithQualityHeaderValue("application/json")
        );
        try
        {
            using HttpResponseMessage response = await _client.SendAsync(request);

            response.EnsureSuccessStatusCode();
            var stringData = await response.Content.ReadAsByteArrayAsync();
            var deviceAuthResponse = JsonSerializer.Deserialize<DeviceAuthResponse>(stringData);

            if (String.IsNullOrWhiteSpace(deviceAuthResponse?.device_code))
            {
                throw new InvalidOperationException("Failed to receive device_code for authentication");
            }
            return deviceAuthResponse;
        }
        catch (HttpRequestException ex)
        {
            throw new InvalidOperationException("Failed to initiate GitHub device authorization.", ex);
        }
    }

    //poll the user_access token endpoint as long as expiration allows and user did not supply the verification id 
    public async Task<string> GetUserAuthTokenAsync(DeviceAuthResponse authResponse)
    {
        var userAuthResponse = new UserAuthResponse();
        var device_code = authResponse.device_code;
        var grant_type = "urn:ietf:params:oauth:grant-type:device_code";
        var endpoint = "https://github.com/login/oauth/access_token";
        int interval = authResponse.interval;

        DateTimeOffset expires_at = DateTimeOffset.UtcNow.AddSeconds(authResponse.expires_in);
        //Prompt the user to enter the user_code in the verficiation_uri -> implement later in designated controller
        //Continuously poll github api endpoint 
        while (DateTimeOffset.UtcNow <= expires_at)
        {
            await Task.Delay(TimeSpan.FromSeconds(interval));
            if (DateTimeOffset.UtcNow >= expires_at)
                break;
            using var request = new HttpRequestMessage(
                HttpMethod.Post,
                endpoint
            );
            request.Headers.Accept.ParseAdd("application/json");
            request.Content = new FormUrlEncodedContent(
                new Dictionary<String, String>
                {
                    ["client_id"] = _clientId,
                    ["device_code"] = device_code,
                    ["grant_type"] = grant_type
                }
            );
            using var response = await _client.SendAsync(request);
            response.EnsureSuccessStatusCode();
            var stringData = await response.Content.ReadAsByteArrayAsync();
            userAuthResponse = JsonSerializer.Deserialize<UserAuthResponse>(stringData);
            if (!String.IsNullOrEmpty(userAuthResponse?.access_token))
                return userAuthResponse.access_token;

            switch (userAuthResponse?.error)
            {
                case "authorization_pending":
                    continue;
                case "slow_down":
                    interval = userAuthResponse?.interval ?? interval + 5;
                    continue;
                default:
                    throw new InvalidOperationException(
                    $"GitHub OAuth Error: {userAuthResponse?.error ?? "Unknown error"}"
                );
            }
        }
        throw new TimeoutException("GitHub device authorization expired before completion.");
    }

    public async Task<List<(long installationId, string accountLogin)>> GetInstallationsAsync(string access_token, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            "https://api.github.com/user/installations"
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access_token);
        request.Headers.UserAgent.ParseAdd("VictoriousSecretCLI/1.0");
        request.Headers.Accept.ParseAdd("application/vnd.github+json");

        using var response = await _client.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();

        using var json = await JsonDocument.ParseAsync(
            await response.Content.ReadAsStreamAsync(ct),
            cancellationToken: ct
        );

        return json.RootElement
        .GetProperty("installations")
        .EnumerateArray()
        .Select(i => (
            Id: i.GetProperty("id").GetInt64(),
            AccountLogin: i.GetProperty("account").GetProperty("login").GetString()!
        ))
        .ToList();
    }
}