using System.Text.RegularExpressions;

namespace Quayside.Api.Orchestration;

public static partial class CapabilityQuestion
{
    public static readonly string[] Reply =
    [
        "I answer questions about MSC from its public posts and web pages, and I cite a source for every fact so you can check it.\n\n",
        "I can also query a synthetic demo shipping database for containers, vessels, ports, schedules and aggregate figures. That data is made up for demonstration; it is not live MSC information.\n\n",
        "Things worth asking me:\n\n",
        "- What is MSC doing about alternative marine fuels?\n",
        "- What does MSC say about reefer and cold chain solutions?\n",
        "- Where are MSC Technology's offices?\n",
        "- Which five ports have the most container movements?\n\n",
        "If something is not in the material I hold, I will say so rather than guess.",
    ];

    public static bool Matches(string question)
    {
        var text = question.Trim();
        if (text.Length == 0)
        {
            return false;
        }

        if (Greeting().IsMatch(text))
        {
            return true;
        }

        return text.Length <= 110 && Capability().IsMatch(text) && !SpecificSubject().IsMatch(text);
    }

    [GeneratedRegex(@"^(?:hi|hey|hello|yo|hiya|good\s+(?:morning|afternoon|evening)|greetings)[\s,!.?]*$", RegexOptions.IgnoreCase)]
    private static partial Regex Greeting();

    [GeneratedRegex(
        @"\b(?:what\s+(?:can|do|are)\s+(?:you|u)|what(?:'?s| is)\s+this|who\s+(?:are|built|made)\s+you|how\s+do\s+you\s+work|what\s+data\s+do\s+you|your\s+capabilities|can\s+you\s+help|what\s+do\s+you\s+know)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Capability();

    [GeneratedRegex(
        @"\d|\b(?:profit|revenue|turnover|earnings|fleet|office|offices|port|ports|vessel|vessels|container|containers|schedule|schedules|reefer|cargo|founded|ceo|employees|headquarters|fuel|fuels|emission|emissions)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex SpecificSubject();
}
