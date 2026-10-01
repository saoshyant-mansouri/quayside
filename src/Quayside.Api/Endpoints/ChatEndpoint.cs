using Microsoft.AspNetCore.Http.Features;
using Microsoft.Extensions.Options;
using Quayside.Api.Contract;
using Quayside.Api.Orchestration;
using Quayside.Api.Streaming;

namespace Quayside.Api.Endpoints;

public static class ChatEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) => routes.MapPost("/api/chat", HandleAsync);

    private static async Task<IResult> HandleAsync(
        ChatRequest? request,
        HttpContext http,
        ChatOrchestrator orchestrator,
        IOptions<ChatLimits> limits)
    {
        if (request is null || string.IsNullOrWhiteSpace(request.Message))
        {
            return Results.BadRequest(new ProblemPayload("message is required."));
        }

        if (request.Message.Length > limits.Value.MaxMessageLength)
        {
            return Results.BadRequest(new ProblemPayload($"message must be at most {limits.Value.MaxMessageLength} characters."));
        }

        var response = http.Response;
        response.ContentType = "text/event-stream; charset=utf-8";
        response.Headers.CacheControl = "no-cache, no-transform";
        response.Headers["X-Accel-Buffering"] = "no";
        http.Features.Get<IHttpResponseBodyFeature>()?.DisableBuffering();
        await response.StartAsync(http.RequestAborted);

        await orchestrator.RunAsync(request, new SseStreamSink(response.Body), http.RequestAborted);
        return Results.Empty;
    }
}
