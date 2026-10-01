using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace Quayside.Api.Orchestration;

public sealed class ToolCallingChat
{
    public ToolCallingChat(IChatClient inner, IOptions<ChatLimits> limits)
    {
        var invoking = new ChatClientBuilder(inner).UseKernelFunctionInvocation().Build();
        if (invoking.GetService<FunctionInvokingChatClient>() is { } function)
        {
            function.MaximumIterationsPerRequest = Math.Max(limits.Value.MaxToolIterations, 1);
        }

        Service = invoking.AsChatCompletionService();
    }

    public IChatCompletionService Service { get; }
}
