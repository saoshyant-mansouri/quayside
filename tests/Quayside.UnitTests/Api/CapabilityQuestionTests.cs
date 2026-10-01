using Quayside.Api.Orchestration;

namespace Quayside.UnitTests.Api;

public sealed class CapabilityQuestionTests
{
    [Theory]
    [InlineData("what can you really answer")]
    [InlineData("What can you do?")]
    [InlineData("what do you know")]
    [InlineData("Who built you?")]
    [InlineData("how do you work")]
    [InlineData("hi, what can you answer?")]
    [InlineData("hi")]
    [InlineData("hello!")]
    [InlineData("what can you do and tell me about msc")]
    [InlineData("can you help")]
    public void Questions_about_the_assistant_are_recognised(string question) =>
        Assert.True(CapabilityQuestion.Matches(question));

    [Theory]
    [InlineData("What is MSC doing about alternative marine fuels?")]
    [InlineData("Where are MSC Technology's offices?")]
    [InlineData("Which five ports have the most container movements?")]
    [InlineData("Track container MSCU1234567.")]
    [InlineData("What do you know about MSC's net profit in 2024 and its fleet size across every trade lane worldwide?")]
    public void Questions_about_MSC_are_not_recognised(string question) =>
        Assert.False(CapabilityQuestion.Matches(question));

    [Fact]
    public void The_reply_names_concrete_questions_and_admits_its_limits()
    {
        var reply = string.Concat(CapabilityQuestion.Reply);
        Assert.Contains("cite", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("synthetic", reply, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("MSC Technology's offices", reply, StringComparison.Ordinal);
    }
}
