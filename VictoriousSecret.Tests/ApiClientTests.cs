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
    [Theory]
    [InlineData("{\"message\":\"queued\",\"scanJobs\":[]}")]
    [InlineData("{\"message\":\"queued\",\"scanJobs\":[],\"notSubmitted\":{},\"submissionError\":null}")]
    public async Task OldAndNewSubmissionResponsesAreCompatible(string body)
    {
        using var client = new HttpClient(new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        }));
        var result = await new ScanApiClient(client).RequestAllAsync(42, "token");
        Assert.Empty(result.NotSubmitted);
        Assert.Null(result.SubmissionError);
    }

    [Theory]
    [InlineData("{\"message\":\"queued\",\"scanJobs\":[],\"notSubmitted\":null}")]
    [InlineData("{\"message\":\"queued\",\"scanJobs\":[],\"notSubmitted\":{\"owner/repo\":null}}")]
    public async Task InvalidSubmissionProblemsAreRejected(string body)
    {
        using var client = new HttpClient(new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        }));
        await Assert.ThrowsAsync<System.IO.InvalidDataException>(() => new ScanApiClient(client).RequestAllAsync(42, "token"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"message\":\"queued\"}")]
    public async Task SubmissionRejectsMissingRequiredData(string body)
    {
        using var client = new HttpClient(new FakeHandler(new HttpResponseMessage(HttpStatusCode.Accepted)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        }));
        await Assert.ThrowsAsync<System.IO.InvalidDataException>(() =>
            new ScanApiClient(client).RequestAllAsync(42, "token"));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("{\"findings\":[],\"failed\":null}")]
    [InlineData("{\"findings\":[{}],\"failed\":{}}")]
    public async Task CompletedFindingsRejectMissingRequiredData(string body)
    {
        using var client = new HttpClient(new FakeHandler(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json")
        }));
        await Assert.ThrowsAsync<System.IO.InvalidDataException>(() =>
            new ScanApiClient(client).GetFindingsAsync([Guid.NewGuid()], "token", CancellationToken.None));
    }

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
