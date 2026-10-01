namespace Quayside.UnitTests.Api;

public static class Questions
{
    public const string Grounded = "What connections does MSC offer from Canada to Europe?";

    public const string Ranking = "Which five ports had the most import containers last quarter?";

    public const string AcceptedSql = "SELECT TOP (5) p.PortName FROM ops.Ports p ORDER BY p.PortName";

    public const string Container = "Track container MSCU1234567.";

    public const string Destructive = "Which bookings should we delete from the top of the list?";

    public const string Unanswerable = "Which port will have the best weather forecast tomorrow?";

    public const string UnknownFigure = "What was MSC's net profit in 2024?";

    public static string? SqlFor(string question) => question == Ranking ? AcceptedSql : null;

    public static TheoryData<string> Scenarios() => new()
    {
        Grounded,
        Ranking,
        Container,
        Destructive,
        UnknownFigure,
    };
}
