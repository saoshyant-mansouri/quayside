using Quayside.Api.Contract;

namespace Quayside.Api.Endpoints;

public static class ExamplesEndpoint
{
    public static IReadOnlyList<ExamplePayload> Examples { get; } =
    [
        new("Sustainability", "What is MSC doing about alternative marine fuels and decarbonisation?"),
        new("Services", "What does MSC say about reefer and cold chain solutions?"),
        new("Demo database", "Which five ports have the most container movements?"),
        new("Offices", "Where are MSC Technology's offices?"),
    ];

    public static void Map(IEndpointRouteBuilder routes) => routes.MapGet("/api/examples", () => Results.Ok(Examples));
}
