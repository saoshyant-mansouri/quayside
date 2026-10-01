using Quayside.Api.Contract;
using Quayside.Api.Orchestration;

namespace Quayside.Api.Tools;

public sealed class ToolRunner(ILogger<ToolRunner> logger)
{
    public async Task<string> RunAsync(TurnContext turn, string name, Func<Task<ToolOutcome>> work, CancellationToken ct)
    {
        turn.Gate.DiscardPending();
        await turn.EmitAsync(new ToolEvent(name, "started"), ct);

        ToolOutcome outcome;
        try
        {
            outcome = await work();
        }
        catch (InvalidToolInputException ex)
        {
            outcome = new ToolOutcome($"INVALID_INPUT: {ex.Message}", new Dictionary<string, object?> { ["invalid"] = true });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Tool {Tool} failed", name);
            outcome = new ToolOutcome(
                "TOOL_ERROR: the tool could not complete. Tell the user it is unavailable right now.",
                new Dictionary<string, object?> { ["error"] = true });
        }

        await turn.EmitAsync(new ToolEvent(name, "completed", outcome.Detail), ct);
        return outcome.ForModel;
    }
}
