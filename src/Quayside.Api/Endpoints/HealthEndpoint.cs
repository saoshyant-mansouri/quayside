using Quayside.Api.Contract;
using Quayside.Api.Hosting;

namespace Quayside.Api.Endpoints;

public static class HealthEndpoint
{
    public static void Map(IEndpointRouteBuilder routes) =>
        routes.MapGet("/api/health", (Hydrated<CorpusIndex> corpus) =>
        {
            var warm = corpus.Value;
            return Results.Ok(new HealthPayload(
                "ok",
                warm?.Index.ChunkCount ?? 0,
                warm?.Index.DocumentCount ?? 0,
                warm?.CapturedAt?.UtcDateTime,
                warm is not null));
        });
}
