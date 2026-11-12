using System.CommandLine;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

public class CliTests
{
    private static HttpClient CreateClient(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("http://localhost:5000/")
    };

    private sealed class MemoryTokenStore : ITokenStore
    {
        public string? Token { get; private set; }
        public MemoryTokenStore(string? token = null) => Token = token;
        public void SaveToken(string token) => Token = token;
        public string? GetToken() => Token;
        public bool Remove()
        {
            bool present = Token is not null;
            Token = null;
            return present;
        }
    }

    private sealed class FakeHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
        : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
            => send(request, ct);
    }

    private static HttpResponseMessage Json(object value, HttpStatusCode status = HttpStatusCode.OK)
        => new(status) { Content = JsonContent.Create(value) };

    private static HttpResponseMessage Installations() => Json(new
    {
        installations = new[] { new { id = 42L, account = new { login = "owner" } } }
    });

    private static HttpClient NoNetworkClient() => CreateClient(new FakeHandler((_, _) =>
        throw new InvalidOperationException("Unexpected HTTP request.")));

    [Theory]
    [InlineData(true, true, 1)]
    [InlineData(false, true, 1)]
    [InlineData(false, false, 0)]
    public async Task SubmissionProblemsAreDisplayedAndOnlyAcceptedJobsArePolled(bool accepted, bool incomplete, int exitCode)
    {
        var jobId = Guid.NewGuid();
        int calls = 0;
        using var client = CreateClient(new FakeHandler(async (request, ct) =>
        {
            switch (++calls)
            {
                case 1: return Installations();
                case 2:
                    return Json(new
                    {
                        message = "submission finished",
                        scanJobs = accepted ? new[] { jobId } : Array.Empty<Guid>(),
                        notSubmitted = incomplete ? new Dictionary<string, string> { ["owner/missing"] = "inaccessible" } : new(),
                        submissionError = incomplete ? "Submission timed out." : null
                    }, accepted ? HttpStatusCode.Accepted : HttpStatusCode.OK);
                default:
                    Assert.True(accepted);
                    var ids = JsonSerializer.Deserialize<Guid[]>(await request.Content!.ReadAsStringAsync(ct));
                    Assert.Equal(new[] { jobId }, ids);
                    return Json(new { findings = Array.Empty<object>(), failed = new Dictionary<Guid, string>() });
            }
        }));
        var output = new StringWriter();
        var handler = new ScanHandler(new GitHubUserAuth("client", client), new ScanApiClient(client),
            new MemoryTokenStore("token"), new StringReader("1"), output);
        Assert.Equal(exitCode, await handler.HandleAsync(true, null));
        Assert.Equal(accepted ? 3 : 2, calls);
        Assert.Contains("60 seconds", output.ToString());
        if (incomplete)
        {
            Assert.Contains("owner/missing", output.ToString());
            Assert.Contains("Submission timed out.", output.ToString());
        }
        if (!accepted) Assert.DoesNotContain("Polling", output.ToString());
    }

    [Fact]
    public async Task LoginSavesTokenAndLogoutRemovesIt()
    {
        int calls = 0;
        using var client = CreateClient(new FakeHandler(async (request, ct) =>
        {
            Assert.Equal(HttpMethod.Post, request.Method);
            if (++calls == 1)
            {
                Assert.Equal("/login/device/code", request.RequestUri!.AbsolutePath);
                return Json(new { device_code = "device", user_code = "user", verification_uri = "https://github.com/login/device", expires_in = 60, interval = 0 });
            }
            Assert.Equal("/login/oauth/access_token", request.RequestUri!.AbsolutePath);
            Assert.Contains("device_code=device", await request.Content!.ReadAsStringAsync(ct));
            return Json(new { access_token = "new-token" });
        }));
        var store = new MemoryTokenStore("old-token");
        var handler = new AuthHandler(new GitHubUserAuth("client", client), store, new StringWriter());
        Assert.Equal(0, await handler.LoginAsync());
        Assert.Equal("new-token", store.GetToken());
        Assert.Equal(0, handler.Logout());
        Assert.Null(store.GetToken());
        Assert.Equal(1, handler.Logout());
    }

    [Theory]
    [InlineData("scan")]
    [InlineData("scan --all --repo owner/repo")]
    [InlineData("scan --repo owner/repo")]
    [InlineData("auth status")]
    [InlineData("scan --all")]
    public async Task InvalidOrUnimplementedCommandsReturnFailureWithoutNetwork(string command)
    {
        using var client = NoNetworkClient();
        var auth = new GitHubUserAuth("client", client);
        var store = new MemoryTokenStore();
        var output = new StringWriter();
        var root = VSCli.CreateRootCommand(new AuthHandler(auth, store, output),
            new ScanHandler(auth, new ScanApiClient(client), store, new StringReader("1"), output));
        Assert.Equal(1, await root.Parse(command).InvokeAsync());
        if (command is "auth status" or "scan --repo owner/repo")
            Assert.Contains("not implemented yet", output.ToString());
    }

    [Fact]
    public async Task HelpPreservesAdvertisedCommandsAndOptions()
    {
        using var client = NoNetworkClient();
        var auth = new GitHubUserAuth("client", client);
        var store = new MemoryTokenStore();
        var root = VSCli.CreateRootCommand(new AuthHandler(auth, store), new ScanHandler(auth, new ScanApiClient(client), store));
        Assert.Equal(0, await root.Parse("auth --help").InvokeAsync());
        Assert.Equal(0, await root.Parse("scan --help").InvokeAsync());
        var authCommand = Assert.Single(root.Subcommands, c => c.Name == "auth");
        Assert.Contains(authCommand.Subcommands, c => c.Name == "status");
        var scanCommand = Assert.Single(root.Subcommands, c => c.Name == "scan");
        Assert.Contains(scanCommand.Options, o => o.Name == "--repo");
    }

    [Theory]
    [InlineData(false, 0)]
    [InlineData(true, 1)]
    public async Task ScanUsesSelectedInstallationAndPollsResults(bool failed, int expectedExit)
    {
        var jobId = Guid.NewGuid();
        int calls = 0;
        using var client = CreateClient(new FakeHandler(async (request, ct) =>
        {
            Assert.Equal("Bearer", request.Headers.Authorization!.Scheme);
            Assert.Equal("test-token", request.Headers.Authorization.Parameter);
            switch (++calls)
            {
                case 1:
                    Assert.Equal("https://api.github.com/user/installations", request.RequestUri!.ToString());
                    return Installations();
                case 2:
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("http://localhost:5000/api/scan/all?installationId=42", request.RequestUri!.ToString());
                    return Json(new { message = "queued", scanJobs = new[] { jobId } }, HttpStatusCode.Accepted);
                default:
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("/api/scan/findings", request.RequestUri!.AbsolutePath);
                    var sentIds = JsonSerializer.Deserialize<Guid[]>(await request.Content!.ReadAsStringAsync(ct));
                    Assert.Equal(new[] { jobId }, sentIds);
                    if (calls == 3) return new(HttpStatusCode.Accepted);
                    return Json(new
                    {
                        findings = failed ? Array.Empty<SecretFindingDTO>() : new[] { new SecretFindingDTO("src/test.cs", 7, "GitHub", "token", "ProviderPattern") },
                        failed = failed ? new Dictionary<Guid, string> { [jobId] = "repo" } : new Dictionary<Guid, string>()
                    });
            }
        }));
        var output = new StringWriter();
        var handler = new ScanHandler(new GitHubUserAuth("client", client), new ScanApiClient(client),
            new MemoryTokenStore("test-token"), new StringReader("1"), output);
        Assert.Equal(expectedExit, await handler.HandleAsync(true, null));
        Assert.Equal(4, calls);
        Assert.Contains(failed ? "1 failed" : "src/test.cs", output.ToString());
    }

    [Theory]
    [InlineData("0")]
    [InlineData("2")]
    [InlineData("invalid")]
    public async Task InvalidSelectionDoesNotSubmit(string selection)
    {
        int calls = 0;
        using var client = CreateClient(new FakeHandler((_, _) =>
        {
            calls++;
            return Task.FromResult(Installations());
        }));
        var handler = new ScanHandler(new GitHubUserAuth("client", client), new ScanApiClient(client),
            new MemoryTokenStore("token"), new StringReader(selection), new StringWriter());
        Assert.Equal(1, await handler.HandleAsync(true, null));
        Assert.Equal(1, calls);
    }

    [Theory]
    [InlineData(401)]
    [InlineData(503)]
    public async Task HttpErrorsReturnFailure(int status)
    {
        using var client = CreateClient(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage((HttpStatusCode)status))));
        var store = new MemoryTokenStore("token");
        var auth = new GitHubUserAuth("client", client);
        Assert.Equal(1, await new ScanHandler(auth, new ScanApiClient(client), store, output: new StringWriter()).HandleAsync(true, null));
        Assert.Equal(1, await new AuthHandler(auth, store, new StringWriter()).LoginAsync());
        Assert.Equal("token", store.GetToken());
    }

    [Fact]
    public async Task CancellationReachesScanAndLoginRequests()
    {
        using var cts = new CancellationTokenSource();
        using var client = CreateClient(new FakeHandler(async (_, ct) =>
        {
            cts.Cancel();
            await Task.Delay(Timeout.Infinite, ct);
            throw new InvalidOperationException("Unreachable");
        }));
        var auth = new GitHubUserAuth("client", client);
        Assert.Equal(103, await new ScanHandler(auth, new ScanApiClient(client), new MemoryTokenStore("token"), output: new StringWriter())
            .HandleAsync(true, null, cts.Token));
        Assert.Equal(103, await new AuthHandler(auth, new MemoryTokenStore(), new StringWriter()).LoginAsync(cts.Token));
    }

    [Theory]
    [InlineData("null")]
    [InlineData("{}")]
    [InlineData("not-json")]
    public async Task MalformedInstallationsReturnFailure(string body)
    {
        using var client = CreateClient(new FakeHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        { Content = new StringContent(body, System.Text.Encoding.UTF8, "application/json") })));
        var handler = new ScanHandler(new GitHubUserAuth("client", client), new ScanApiClient(client),
            new MemoryTokenStore("token"), output: new StringWriter());
        Assert.Equal(1, await handler.HandleAsync(true, null));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task OAuthDenialOrMalformedDeviceResponseDoesNotSaveToken(bool denied)
    {
        int calls = 0;
        using var client = CreateClient(new FakeHandler((_, _) => Task.FromResult(++calls == 1 && denied
            ? Json(new { device_code = "device", user_code = "user", verification_uri = "https://github.com/login/device", expires_in = 60, interval = 0 })
            : Json(new { error = "access_denied" }))));
        var store = new MemoryTokenStore();
        Assert.Equal(1, await new AuthHandler(new GitHubUserAuth("client", client), store, new StringWriter()).LoginAsync());
        Assert.Null(store.GetToken());
    }
}
