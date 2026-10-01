namespace Quayside.Api.Contract;

public sealed record ChatRequest(string? Message, string? ConversationId);
