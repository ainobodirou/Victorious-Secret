using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

public class ApiClientTests
{
    [Fact]
    public async Task GetFindingsWhenProcessingReturnsNull()
    {
        using var response = new HttpResponseMessage(HttpStatusCode.Accepted);
        using var HttpClient = new HttpClient(new FakeHandler(response));
        var scanClient = new ScanApiClient(HttpClient);
        List<Guid> jobIds = [Guid.NewGuid()];

        var results = await scanClient.GetFindingsAsync(
            jobIds, "test-token", CancellationToken.None
        );
        Assert.Null(results);
    }

    [Fact]
    public async Task GetFindingsWhenCompletedReturnsNull()
    {   
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                findings = Array.Empty<object>(),
                failed = new Dictionary<Guid, string>()
            })
        };
        using var HttpClient = new HttpClient(new FakeHandler(response));
        var scanClient = new ScanApiClient(HttpClient);

        List<Guid> jobIds = [Guid.NewGuid()];

        FindingsResponse? result = await scanClient.GetFindingsAsync(
            jobIds, "test-token", CancellationToken.None
        );

        Assert.NotNull(result);
        Assert.Empty(result.Findings);
        Assert.Empty(result.Failed);
    }
    [Fact]
    public async Task GetFindings_WhenSecretFound_OneFinding()
    {
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = JsonContent.Create(new
            {
                findings = new[]
                {
                    new
                    {
                        FilePath =  "src/Config/cs",
                        LineNumber = 14,
                        Provider = "GitLab",
                        CredentialType = "API key",
                        DetectionMethod = "Generic"
                    }
                },
                failed = new Dictionary<Guid, string>()
            })
        };
        using var HttpClient = new HttpClient(new FakeHandler(response));
        var scanClient = new ScanApiClient(HttpClient);

        FindingsResponse? result = await scanClient.GetFindingsAsync([Guid.NewGuid()], "test-token", CancellationToken.None);

        Assert.NotNull(result);
        SecretFindingDTO finding = Assert.Single(result.Findings);
        Assert.Empty(result.Failed);
        Assert.Equal("src/Config/cs", finding.FilePath);
        Assert.Equal(14, finding.LineNumber);
}
    


private sealed class FakeHandler (HttpResponseMessage response): HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken token
    )
    {
        return Task.FromResult(response);
    }
}
}
