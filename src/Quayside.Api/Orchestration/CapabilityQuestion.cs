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
        if (text.Length > 80 || Subject().IsMatch(text))
        {
            return false;
        }

        return Pattern().IsMatch(text);
    }

    [GeneratedRegex(@"\b(?:msc|container|vessel|port|schedule|cargo|shipping|reefer|profit|fleet|office)\w*\b|\babout\s+\w", RegexOptions.IgnoreCase)]
    private static partial Regex Subject();

    [GeneratedRegex(
        @"^(?:hi|hello|hey)?[\s,!.]*(?:(?:so\s+)?what(?:'?s| is| are| can| do)?\s+(?:you|u|this|it|yours)?\s*(?:can|could|able to)?\s*(?:really\s+)?(?:do|answer|answers|help|know|offer|tell)|what\s+(?:can|do)\s+you|what\s+(?:are|is)\s+(?:you|this)|who\s+(?:are|built|made)\s+you|how\s+do\s+you\s+work|what\s+data\s+do\s+you|capabilities|help)\b",
        RegexOptions.IgnoreCase)]
    private static partial Regex Pattern();
}
