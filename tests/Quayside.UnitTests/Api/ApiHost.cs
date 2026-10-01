using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Quayside.Api;
using Quayside.Api.Offline;
using Quayside.Api.Orchestration;
using Quayside.Core;

namespace Quayside.UnitTests.Api;

public sealed class ApiSettings
{
    public double? MinTopCosine { get; init; }

    public int? MaxConversations { get; init; }

    public int? MaxTurnsPerConversation { get; init; }

    public int? WarmupWaitSeconds { get; init; }

    public bool HoldHydration { get; init; }

    public bool WebEnabled { get; init; }
}

public sealed class ApiHost(ApiSettings? settings = null) : WebApplicationFactory<Program>
{
    public const string Secret = "Server=tcp:quayside-sentinel.database.windows.net;Password=sentinel-hunter2";

    private readonly ApiSettings settings = settings ?? new ApiSettings();
    private readonly TaskCompletionSource hydration = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private HttpClient? client;

    public ModelProbe Model { get; } = new();

    public CountingSqlExecutor Sql { get; } = new();

    public FakeWebSearch Web { get; } = new();

    public CapturedLogs Logs { get; } = new();

    public HttpClient Client => client ??= CreateClient();

    public void ReleaseHydration() => hydration.TrySetResult();

    public async Task<ChatResult> AskAsync(string message, string? conversationId = null, CancellationToken ct = default)
    {
        using var request = ChatRequest(message, conversationId);
        using var response = await Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        var raw = await response.Content.ReadAsStringAsync(ct);
        return ChatResult.From(response.StatusCode, response.Content.Headers.ContentType?.MediaType, raw);
    }

    public static HttpRequestMessage ChatRequest(string message, string? conversationId = null) =>
        new(HttpMethod.Post, "/api/chat") { Content = JsonContent.Create(new { message, conversationId }) };

    public async Task<JsonElement> HealthAsync()
    {
        using var response = await Client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    public async Task<ChatResult> AskWarmAsync(string message, string? conversationId = null)
    {
        await WaitUntilWarmAsync();
        return await AskAsync(message, conversationId);
    }

    public async Task WaitUntilWarmAsync()
    {
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
        while (!(await HealthAsync()).GetProperty("indexWarm").GetBoolean())
        {
            await Task.Delay(25, timeout.Token);
        }
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting(OfflineServices.Flag, "true");
        builder.UseSetting("ConnectionStrings:SqlReadOnly", Secret);
        builder.ConfigureLogging(logging => logging.AddProvider(Logs));
        builder.ConfigureTestServices(Replace);
    }

    private void Replace(IServiceCollection services)
    {
        services.Replace(ServiceDescriptor.Singleton<IChatClient>(Model));
        services.Replace(ServiceDescriptor.Singleton<IReadOnlySqlExecutor>(Sql));
        if (settings.WebEnabled)
        {
            services.Replace(ServiceDescriptor.Singleton<IWebSearch>(Web));
        }

        services.Replace(ServiceDescriptor.Singleton<IChunkStore>(provider => new GatedChunkStore(
            ActivatorUtilities.CreateInstance<OfflineChunkStore>(provider),
            settings.HoldHydration ? hydration.Task : Task.CompletedTask)));

        services.PostConfigure<GroundingOptions>(options => options.MinTopCosine = settings.MinTopCosine ?? options.MinTopCosine);
        services.PostConfigure<ChatLimits>(options =>
        {
            options.MaxConversations = settings.MaxConversations ?? options.MaxConversations;
            options.MaxTurnsPerConversation = settings.MaxTurnsPerConversation ?? options.MaxTurnsPerConversation;
            options.WarmupWaitSeconds = settings.WarmupWaitSeconds ?? options.WarmupWaitSeconds;
        });
    }
}
