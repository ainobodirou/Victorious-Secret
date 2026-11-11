using System.Net;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Octokit;
using Octokit.Internal;

public class SubmissionTests
{
    private sealed class Transport(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct) => send(request);
    }

    private static HttpResponseMessage Json(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    private static HttpResponseMessage Repositories(params string[] names) => Json(JsonSerializer.Serialize(new
    {
        total_count = names.Length,
        repositories = names.Select(name => new { id = name.Length, name, owner = new { login = "owner" }, default_branch = "main" })
    }));

    private static HttpResponseMessage Branch() => Json("{\"commit\":{\"sha\":\"abc123\"}}");

    private static GitHubClientO Wrapper(Transport transport)
    {
        var client = new Octokit.GitHubClient(new Connection(new ProductHeaderValue("tests"), new HttpClientAdapter(() => transport)));
        var provider = new GitHubTokenProvider(Options.Create(new GitHubAppOptions()), new HttpClient(transport));
        return new GitHubClientO(provider, _ => Task.FromResult(client));
    }

    private static ScanRequestResult Result(IActionResult action, int status)
    {
        var result = Assert.IsAssignableFrom<ObjectResult>(action);
        Assert.Equal(status, result.StatusCode);
        return JsonSerializer.Deserialize<ScanRequestResult>(JsonSerializer.Serialize(result.Value), new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
    }

    [Fact]
    public async Task MissingAndForbiddenRepositoriesAreReportedWhileOthersAreQueued()
    {
        using var transport = new Transport(request => Task.FromResult(request.RequestUri!.AbsolutePath switch
        {
            "/installation/repositories" => Repositories("good", "missing", "forbidden", "last"),
            "/repos/owner/missing/branches/main" => Json("{\"message\":\"Not found\"}", HttpStatusCode.NotFound),
            "/repos/owner/forbidden/branches/main" => Json("{\"message\":\"Forbidden\"}", HttpStatusCode.Forbidden),
            _ => Branch()
        }));
        var store = new ScanJobStore();
        var queue = new ScanJobQueueSevice(10);
        var result = Result(await new ScanController.ScanController(Wrapper(transport), queue, store).RequestScanForAllAsync(42, default), 202);
        Assert.Equal(2, result.jobIds.Count);
        Assert.Equal(2, result.NotSubmitted.Count);
        Assert.Contains("owner/missing", result.NotSubmitted.Keys);
        Assert.Contains("owner/forbidden", result.NotSubmitted.Keys);
        Assert.Null(result.SubmissionError);
        foreach (var id in result.jobIds)
        {
            Assert.Equal(id, (await queue.DequeueScanJobAsync(default)).JobId);
            Assert.Equal(ScanStatus.Queued, store.Get(id)!.Status);
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CredentialRejectionPreservesAcceptedIds(bool duringDiscovery)
    {
        using var transport = new Transport(request => Task.FromResult(
            request.RequestUri!.AbsolutePath == "/installation/repositories" && !duringDiscovery ? Repositories("good", "bad", "later") :
            request.RequestUri.AbsolutePath.Contains("/good/") ? Branch() :
            Json("{\"message\":\"Bad credentials\"}", HttpStatusCode.Unauthorized)));
        var result = Result(await new ScanController.ScanController(Wrapper(transport), new ScanJobQueueSevice(10), new ScanJobStore())
            .RequestScanForAllAsync(42, default), duringDiscovery ? 200 : 202);
        Assert.Equal(duringDiscovery ? 0 : 1, result.jobIds.Count);
        Assert.Equal(duringDiscovery ? 0 : 2, result.NotSubmitted.Count);
        Assert.Contains("401", result.SubmissionError!);
    }

    [Theory]
    [InlineData("rate", true)]
    [InlineData("rate", false)]
    [InlineData("server", false)]
    [InlineData("network", false)]
    [InlineData("timeout", false)]
    public async Task TransientFailuresRetryWithinDeadline(string failure, bool discovery)
    {
        var clock = new ManualTimeProvider();
        int attempts = 0;
        using var transport = new Transport(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/installation/repositories" && !discovery)
                return Task.FromResult(Repositories("good"));
            if (discovery && request.RequestUri.AbsolutePath != "/installation/repositories")
                return Task.FromResult(Branch());
            if (++attempts == 1)
            {
                if (failure == "network") throw new HttpRequestException("Offline");
                if (failure == "timeout") throw new TaskCanceledException("Request timed out");
                var response = Json(failure == "rate" ? "{\"message\":\"API rate limit exceeded for tests.\"}" : "{\"message\":\"Temporary failure\"}", failure == "rate" ? HttpStatusCode.Forbidden : HttpStatusCode.ServiceUnavailable);
                if (failure == "rate")
                {
                    response.Headers.Add("X-RateLimit-Limit", "5000");
                    response.Headers.Add("X-RateLimit-Remaining", "0");
                    response.Headers.Add("X-RateLimit-Reset", clock.GetUtcNow().AddSeconds(2).ToUnixTimeSeconds().ToString());
                }
                return Task.FromResult(response);
            }
            return Task.FromResult(discovery ? Repositories("good") : Branch());
        });
        // A discovery retry is followed by a separate, successful branch lookup.
        var action = new ScanController.ScanController(Wrapper(transport), new ScanJobQueueSevice(10), new ScanJobStore(), clock)
            .RequestScanForAllAsync(42, default);
        await clock.WaitForTimerAsync(TimeSpan.FromSeconds(2));
        clock.Advance(TimeSpan.FromSeconds(2));
        var result = Result(await action.WaitAsync(TimeSpan.FromSeconds(5)), 202);
        Assert.Single(result.jobIds);
        Assert.True(attempts >= 2);
        Assert.Null(result.SubmissionError);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DeadlineBoundsInFlightReadAndPreservesAcceptedIds(bool discovery)
    {
        var clock = new ManualTimeProvider();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var pending = new TaskCompletionSource<HttpResponseMessage>(TaskCreationOptions.RunContinuationsAsynchronously);
        using var transport = new Transport(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/installation/repositories" && !discovery)
                return Task.FromResult(Repositories("good", "slow", "later"));
            if (request.RequestUri.AbsolutePath.Contains("/good/")) return Task.FromResult(Branch());
            started.TrySetResult();
            return pending.Task;
        });
        var action = new ScanController.ScanController(Wrapper(transport), new ScanJobQueueSevice(10), new ScanJobStore(), clock)
            .RequestScanForAllAsync(42, default);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(5));
        clock.Advance(TimeSpan.FromSeconds(60));
        var result = Result(await action.WaitAsync(TimeSpan.FromSeconds(5)), discovery ? 200 : 202);
        Assert.Equal(discovery ? 0 : 1, result.jobIds.Count);
        Assert.Equal(discovery ? 0 : 2, result.NotSubmitted.Count);
        Assert.Contains("60 seconds", result.SubmissionError!);
        pending.SetResult(discovery ? Repositories() : Branch());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task FullQueueCleansUpUnqueuedJobOnDeadlineOrCallerCancellation(bool callerCancellation)
    {
        var clock = new ManualTimeProvider();
        var store = new ScanJobStore();
        var queue = new ScanJobQueueSevice(1);
        using var cts = new CancellationTokenSource();
        using var transport = new Transport(request => Task.FromResult(
            request.RequestUri!.AbsolutePath == "/installation/repositories" ? Repositories("first", "second", "third") : Branch()));
        var action = new ScanController.ScanController(Wrapper(transport), queue, store, clock).RequestScanForAllAsync(42, cts.Token);
        // Queue writes and fake HTTP complete synchronously until the second job waits for space.
        Assert.False(action.IsCompleted);
        if (callerCancellation)
        {
            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(() => action);
        }
        else
        {
            clock.Advance(TimeSpan.FromSeconds(60));
            var result = Result(await action.WaitAsync(TimeSpan.FromSeconds(5)), 202);
            Assert.Single(result.jobIds);
            Assert.Equal(2, result.NotSubmitted.Count);
        }
        var queued = await queue.DequeueScanJobAsync(default);
        Assert.Equal("first", queued.Name);
        Assert.Equal(ScanStatus.Queued, store.Get(queued.JobId)!.Status);
        // Inspect the private dictionary only to verify that no orphan remains queued.
        var jobs = (System.Collections.Concurrent.ConcurrentDictionary<Guid, ScanJob>)typeof(ScanJobStore)
            .GetField("_jobs", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(store)!;
        var unqueued = Assert.Single(jobs.Values, job => job.Name == "second");
        Assert.Equal(ScanStatus.Failed, unqueued.Status);
        Assert.DoesNotContain(jobs.Values, job => job.Name == "third");
    }

    [Fact]
    public async Task ClosedQueueReturnsStructuredResultWithoutRetrying()
    {
        var queue = new ScanJobQueueSevice(10);
        using var transport = new Transport(request =>
        {
            if (request.RequestUri!.AbsolutePath == "/installation/repositories") return Task.FromResult(Repositories("first", "second", "third"));
            if (request.RequestUri.AbsolutePath.Contains("/second/")) queue.Complete();
            return Task.FromResult(Branch());
        });
        var result = Result(await new ScanController.ScanController(Wrapper(transport), queue, new ScanJobStore()).RequestScanForAllAsync(42, default), 202);
        Assert.Single(result.jobIds);
        Assert.Equal(2, result.NotSubmitted.Count);
        Assert.Contains("queue", result.SubmissionError!);
    }

    [Fact]
    public async Task NoRepositoriesReturnsSuccessfulEmptyResult()
    {
        using var transport = new Transport(_ => Task.FromResult(Repositories()));
        var result = Result(await new ScanController.ScanController(Wrapper(transport), new ScanJobQueueSevice(1), new ScanJobStore()).RequestScanForAllAsync(42, default), 200);
        Assert.Empty(result.jobIds);
        Assert.Empty(result.NotSubmitted);
        Assert.Null(result.SubmissionError);
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = DateTimeOffset.UtcNow;
        private readonly List<ManualTimer> _timers = [];
        private readonly Channel<TimeSpan> _created = Channel.CreateUnbounded<TimeSpan>();
        public override DateTimeOffset GetUtcNow() => _now;
        public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
        {
            var timer = new ManualTimer(this, callback, state);
            lock (_timers) _timers.Add(timer);
            timer.Change(dueTime, period);
            _created.Writer.TryWrite(dueTime);
            return timer;
        }
        public async Task WaitForTimerAsync(TimeSpan due)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            while (await _created.Reader.ReadAsync(timeout.Token) is var value)
                if (Math.Abs((value - due).TotalSeconds) < 1.1) return;
        }
        public void Advance(TimeSpan amount)
        {
            _now += amount;
            ManualTimer[] timers;
            lock (_timers) timers = _timers.ToArray();
            foreach (var timer in timers) timer.Fire(_now);
        }
        private sealed class ManualTimer(ManualTimeProvider clock, TimerCallback callback, object? state) : ITimer
        {
            private DateTimeOffset? _due;
            public bool Change(TimeSpan dueTime, TimeSpan period)
            {
                _due = dueTime == Timeout.InfiniteTimeSpan ? null : clock.GetUtcNow() + dueTime;
                return true;
            }
            public void Fire(DateTimeOffset now)
            {
                if (_due is { } due && due <= now)
                {
                    _due = null;
                    callback(state);
                }
            }
            public void Dispose() => _due = null;
            public ValueTask DisposeAsync() { Dispose(); return ValueTask.CompletedTask; }
        }
    }
}
