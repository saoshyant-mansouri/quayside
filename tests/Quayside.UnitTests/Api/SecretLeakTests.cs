using System.Text.RegularExpressions;

namespace Quayside.UnitTests.Api;

public sealed partial class SecretLeakTests
{
    [Fact]
    public async Task No_response_body_contains_a_configured_connection_string()
    {
        using var host = new ApiHost();
        host.Model.SqlOverride = Questions.AcceptedSql;
        await host.WaitUntilWarmAsync();

        var bodies = new List<string>();
        foreach (var question in new[] { Questions.Grounded, Questions.Ranking, Questions.Container, Questions.Destructive, Questions.UnknownFigure })
        {
            bodies.Add((await host.AskAsync(question)).Raw);
        }

        bodies.Add(await host.Client.GetStringAsync("/api/health"));
        bodies.Add(await host.Client.GetStringAsync("/api/examples"));

        Assert.All(bodies, body => Assert.DoesNotContain(ApiHost.Secret, body));
        Assert.All(bodies, body => Assert.DoesNotMatch(SecretShapes(), body));
    }

    [Fact]
    public async Task A_failing_turn_leaks_no_secret_shaped_value()
    {
        using var host = new ApiHost();
        host.Model.Behaviour = ModelBehaviour.FailAfterFirstToken;
        host.Model.FailureMessage = ApiHost.Secret;

        var result = await host.AskWarmAsync(Questions.Grounded);

        Assert.DoesNotMatch(SecretShapes(), result.Raw);
    }

    [Fact]
    public async Task A_bad_request_leaks_no_secret_shaped_value()
    {
        using var host = new ApiHost();

        var result = await host.AskAsync(string.Empty);

        Assert.DoesNotMatch(SecretShapes(), result.Raw);
    }

    [Fact]
    public async Task Response_headers_expose_no_server_internals()
    {
        using var host = new ApiHost();
        await host.WaitUntilWarmAsync();

        using var response = await host.Client.GetAsync("/api/health");

        var headers = string.Join('\n', response.Headers.Concat(response.Content.Headers).Select(h => $"{h.Key}: {string.Join(',', h.Value)}"));
        Assert.DoesNotMatch(SecretShapes(), headers);
        Assert.DoesNotContain(ApiHost.Secret, headers);
    }

    [GeneratedRegex(@"(?i)(password|pwd|accountkey|sharedaccesskey|sharedaccesssignature|api[-_]?key|bearer\s+[a-z0-9._-]{12,}|server=tcp:|data source=|sk-[a-z0-9]{20,}|AKIA[0-9A-Z]{16})")]
    private static partial Regex SecretShapes();
}
