
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

public class ScanApiClient
{
    private readonly HttpClient _client;

    public ScanApiClient(
        HttpClient client
    )
    {
        _client = client;
    }

    public async Task<ScanRequestResult> RequestAllAsync(long installationId, string access_token, CancellationToken cancellationToken = default)
    {
        string url = $"http://localhost:5000/api/scan/all?installationId={installationId}";

        using var request = new HttpRequestMessage(
            HttpMethod.Post, url
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access_token);

        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);

        response.EnsureSuccessStatusCode();
        ScanRequestResult? result =
            await response.Content.ReadFromJsonAsync<ScanRequestResult>(cancellationToken);
        if (result is null || result.Message is null || result.jobIds is null
            || result.jobIds.Any(id => id == Guid.Empty)
            || result.NotSubmitted is null
            || result.NotSubmitted.Any(entry => string.IsNullOrWhiteSpace(entry.Key) || string.IsNullOrWhiteSpace(entry.Value)))
            throw new InvalidDataException("Backend returned an invalid scan response.");
        return result;
    }

    public async Task<FindingsResponse?> GetFindingsAsync(
    List<Guid> jobIds,
    string access_token,
    CancellationToken cancellationToken)
    {
        string url = "http://localhost:5000/api/scan/findings";
        using var request = new HttpRequestMessage(
            HttpMethod.Post, url
        );
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", access_token);
        request.Content = JsonContent.Create(jobIds);

        using HttpResponseMessage response = await _client.SendAsync(request, cancellationToken);
        if (response.StatusCode == HttpStatusCode.Accepted)
        {
            //scans in progress - continue polling
            return null;
        }
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<FindingsResponse>(cancellationToken);
        if (result is null || result.Findings is null || result.Failed is null
            || result.Findings.Any(f => f is null || string.IsNullOrWhiteSpace(f.FilePath)
                || f.LineNumber <= 0 || f.Provider is null || f.CredentialType is null || f.DetectionMethod is null)
            || result.Failed.Any(f => f.Key == Guid.Empty || f.Value is null))
            throw new InvalidDataException("Backend returned an invalid findings response.");
        return result;
    }
}

