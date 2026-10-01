using Quayside.Api.Contract;

namespace Quayside.Api.Endpoints;

public static class ExamplesEndpoint
{
    public static IReadOnlyList<ExamplePayload> Examples { get; } =
    [
        new("Grounded RAG", "What is MSC's position on alternative marine fuels?"),
        new("NL to SQL", "Which five ports had the most import containers last quarter?"),
        new("Tool call", "Track container MSCU1234567."),
        new("Correct refusal", "What was MSC's net profit in 2024?"),
    ];

    public static void Map(IEndpointRouteBuilder routes) => routes.MapGet("/api/examples", () => Results.Ok(Examples));
}
