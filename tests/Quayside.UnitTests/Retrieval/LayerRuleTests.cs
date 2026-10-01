using System.Reflection;
using Quayside.Core.Documents;

namespace Quayside.UnitTests.Retrieval;

public sealed class LayerRuleTests
{
    private static readonly string[] ForbiddenPrefixes =
    [
        "Azure.",
        "Microsoft.Data.SqlClient",
        "Microsoft.EntityFrameworkCore",
        "Dapper",
        "Microsoft.SemanticKernel",
        "System.Net.Http"
    ];

    [Fact]
    public void Core_references_no_io_client_assemblies()
    {
        var references = typeof(Document).Assembly.GetReferencedAssemblies()
            .Select(name => name.Name ?? string.Empty)
            .ToArray();

        Assert.NotEmpty(references);
        foreach (var forbidden in ForbiddenPrefixes)
        {
            Assert.DoesNotContain(references, name => name.StartsWith(forbidden, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void Core_keeps_the_pure_libraries_it_is_allowed_to_use()
    {
        var references = typeof(Document).Assembly.GetReferencedAssemblies().Select(name => name.Name).ToArray();

        Assert.Contains("System.Numerics.Tensors", references);
    }
}
