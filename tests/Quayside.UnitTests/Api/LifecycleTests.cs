using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Quayside.Api.Orchestration;

namespace Quayside.UnitTests.Api;

public sealed class LifecycleTests
{
    [Fact]
    public async Task Health_reports_a_cold_index_before_hydration_completes()
    {
        using var host = new ApiHost(new ApiSettings { HoldHydration = true });

        var health = await host.HealthAsync();

        Assert.False(health.GetProperty("indexWarm").GetBoolean());
        Assert.Equal("ok", health.GetProperty("status").GetString());
        Assert.Equal(0, health.GetProperty("chunks").GetInt32());
        Assert.Equal(0, health.GetProperty("documents").GetInt32());
        Assert.Equal(JsonValueKind.Null, health.GetProperty("corpusCapturedAt").ValueKind);
    }

    [Fact]
    public async Task Health_reports_a_warm_index_after_hydration_completes()
    {
        using var host = new ApiHost(new ApiSettings { HoldHydration = true });
        Assert.False((await host.HealthAsync()).GetProperty("indexWarm").GetBoolean());

        host.ReleaseHydration();
        await host.WaitUntilWarmAsync();

        var health = await host.HealthAsync();
        Assert.True(health.GetProperty("indexWarm").GetBoolean());
        Assert.True(health.GetProperty("chunks").GetInt32() > 0);
        Assert.True(health.GetProperty("documents").GetInt32() > 0);
        Assert.True(DateTime.TryParse(health.GetProperty("corpusCapturedAt").GetString(), out _));
    }

    [Fact]
    public async Task Health_uses_the_documented_field_names()
    {
        using var host = new ApiHost();
        await host.WaitUntilWarmAsync();

        var health = await host.HealthAsync();

        var keys = health.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);
        Assert.Equal(["chunks", "corpusCapturedAt", "documents", "indexWarm", "status"], keys);
    }

    [Fact]
    public async Task Health_never_fails_while_the_index_is_warming()
    {
        using var host = new ApiHost(new ApiSettings { HoldHydration = true });

        for (var i = 0; i < 10; i++)
        {
            using var response = await host.Client.GetAsync("/api/health");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
    }

    [Fact]
    public async Task Chat_during_warmup_degrades_visibly_instead_of_failing()
    {
        using var host = new ApiHost(new ApiSettings { HoldHydration = true, WarmupWaitSeconds = 0 });

        var result = await host.AskAsync(Questions.Grounded);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.Empty(result.Of("citations"));
        Assert.Empty(result.Of("error"));
        Assert.Equal("done", result.Names[^1]);
    }

    [Fact]
    public async Task Examples_are_exactly_the_four_documented_ones()
    {
        using var host = new ApiHost();

        using var response = await host.Client.GetAsync("/api/examples");
        var examples = await response.Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var actual = examples.EnumerateArray()
            .Select(e => (e.GetProperty("label").GetString(), e.GetProperty("question").GetString()))
            .ToArray();
        Assert.Equal(
            [
                ("Grounded RAG", "What is MSC's position on alternative marine fuels?"),
                ("NL to SQL", "Which five ports had the most import containers last quarter?"),
                ("Tool call", "Track container MSCU1234567."),
                ("Correct refusal", "What was MSC's net profit in 2024?"),
            ],
            actual);
        Assert.All(examples.EnumerateArray(), e => Assert.Equal(["label", "question"], e.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)));
    }

    [Fact]
    public async Task Client_cancellation_mid_stream_stops_the_work()
    {
        using var host = new ApiHost();
        host.Model.Behaviour = ModelBehaviour.StallAfterFirstToken;
        await host.WaitUntilWarmAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        using var request = ApiHost.ChatRequest(Questions.Grounded);
        using var response = await host.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation.Token);
        var received = await ReadUntilAsync(stream, "event: token", cancellation.Token);
        await host.Model.Stalled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await stream.DisposeAsync();

        await host.Model.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.Contains("event: citations", received);
        Assert.DoesNotContain("event: done", received);
    }

    [Fact]
    public async Task Client_cancellation_is_not_reported_as_a_server_failure()
    {
        using var host = new ApiHost();
        host.Model.Behaviour = ModelBehaviour.StallAfterFirstToken;
        await host.WaitUntilWarmAsync();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(30));

        using var request = ApiHost.ChatRequest(Questions.Grounded);
        using var response = await host.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellation.Token);
        await using var stream = await response.Content.ReadAsStreamAsync(cancellation.Token);
        await ReadUntilAsync(stream, "event: token", cancellation.Token);
        await stream.DisposeAsync();
        await host.Model.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(10));
        await Task.Delay(200);

        Assert.DoesNotContain(host.Logs.Lines, line => line.Category == typeof(ChatOrchestrator).FullName && line.Level >= Microsoft.Extensions.Logging.LogLevel.Error);
    }

    [Fact]
    public async Task A_failure_after_streaming_has_begun_ends_the_stream_with_an_error_event()
    {
        using var host = new ApiHost();
        host.Model.Behaviour = ModelBehaviour.FailAfterFirstToken;

        var result = await host.AskWarmAsync(Questions.Grounded);

        Assert.Equal(HttpStatusCode.OK, result.Status);
        Assert.NotEmpty(result.Of("token"));
        Assert.Equal("error", result.Names[^1]);
        Assert.Empty(result.Of("done"));
        var error = result.Single("error");
        Assert.Equal(["message"], error.Keys);
        Assert.Equal(ChatOrchestrator.GenericFailure, error["message"].GetString());
    }

    [Fact]
    public async Task A_failure_is_logged_server_side_with_its_exception()
    {
        using var host = new ApiHost();
        host.Model.Behaviour = ModelBehaviour.FailAfterFirstToken;

        var result = await host.AskWarmAsync(Questions.Grounded);

        Assert.Equal("error", result.Names[^1]);
        Assert.Contains(host.Logs.Lines, line => line.Level == Microsoft.Extensions.Logging.LogLevel.Error && line.Exception is InvalidOperationException);
    }

    [Fact]
    public async Task A_server_failure_never_exposes_the_exception_message()
    {
        using var host = new ApiHost();
        host.Model.Behaviour = ModelBehaviour.FailAfterFirstToken;
        host.Model.FailureMessage = ApiHost.Secret;

        var result = await host.AskWarmAsync(Questions.Grounded);

        Assert.DoesNotContain(ApiHost.Secret, result.Raw);
        Assert.DoesNotContain("Password", result.Raw);
    }

    private static async Task<string> ReadUntilAsync(Stream stream, string marker, CancellationToken ct)
    {
        var received = new System.Text.StringBuilder();
        var buffer = new byte[1024];
        while (!received.ToString().Contains(marker, StringComparison.Ordinal))
        {
            var read = await stream.ReadAsync(buffer, ct);
            Assert.True(read > 0, "The stream ended before the marker appeared.");
            received.Append(System.Text.Encoding.UTF8.GetString(buffer, 0, read));
        }

        return received.ToString();
    }
}
