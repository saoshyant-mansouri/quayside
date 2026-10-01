namespace Quayside.Api.Tools;

public sealed class InvalidToolInputException(string message) : Exception(message);
