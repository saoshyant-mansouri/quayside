using Microsoft.Extensions.AI;
using Quayside.Api.Contract;

namespace Quayside.Api.Orchestration;

public sealed class UsageTally
{
    private long prompt;
    private long completion;

    public void Add(UsageDetails? details)
    {
        if (details is null)
        {
            return;
        }

        Interlocked.Add(ref prompt, details.InputTokenCount ?? 0);
        Interlocked.Add(ref completion, details.OutputTokenCount ?? 0);
    }

    public UsagePayload ToPayload() => new(Interlocked.Read(ref prompt), Interlocked.Read(ref completion));
}
