using Microsoft.Data.SqlClient;
using Quayside.Infrastructure.Configuration;
using Quayside.Infrastructure.Sql;

namespace Quayside.UnitTests.Infrastructure;

public sealed class ReadOnlySqlExecutorTests
{
    [Fact]
    public void Refuses_a_connection_string_without_read_only_intent()
    {
        var exception = Assert.Throws<InvalidOperationException>(() => new ReadOnlySqlExecutor(InfrastructureFixtures.ReadWrite));

        Assert.Contains("ApplicationIntent=ReadOnly", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Refuses_an_explicit_read_write_intent()
    {
        var connection = InfrastructureFixtures.ReadWrite + ";ApplicationIntent=ReadWrite";

        Assert.Throws<InvalidOperationException>(() => new ReadOnlySqlExecutor(connection));
    }

    [Fact]
    public void Refuses_a_connection_string_that_cannot_be_parsed()
    {
        Assert.Throws<InvalidOperationException>(() => new ReadOnlySqlExecutor("this is not a connection string"));
    }

    [Theory]
    [InlineData("")]
    [InlineData("  ")]
    public void Refuses_a_blank_connection_string(string connection)
    {
        Assert.ThrowsAny<ArgumentException>(() => new ReadOnlySqlExecutor(connection));
    }

    [Fact]
    public void Accepts_a_read_only_connection_string()
    {
        var executor = new ReadOnlySqlExecutor(InfrastructureFixtures.ReadOnly);

        Assert.NotNull(executor);
    }

    [Fact]
    public void The_normalised_connection_keeps_read_only_intent()
    {
        var normalised = ReadOnlyConnectionString.Require(InfrastructureFixtures.ReadOnly);

        Assert.Equal(ApplicationIntent.ReadOnly, new SqlConnectionStringBuilder(normalised).ApplicationIntent);
    }

    [Fact]
    public void The_command_timeout_is_five_seconds()
    {
        Assert.Equal(5, ReadOnlySqlExecutor.CommandTimeoutSeconds);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Refuses_blank_sql(string sql)
    {
        var executor = new ReadOnlySqlExecutor(InfrastructureFixtures.ReadOnly);

        await Assert.ThrowsAnyAsync<ArgumentException>(() => executor.ExecuteAsync(sql, CancellationToken.None));
    }

    [Fact]
    public void The_read_write_database_refuses_a_read_only_connection_string()
    {
        Assert.Throws<ArgumentException>(() => new SqlDatabase(InfrastructureFixtures.ReadOnly));
    }

    [Fact]
    public void The_read_write_database_accepts_the_read_write_connection_string()
    {
        Assert.NotNull(new SqlDatabase(InfrastructureFixtures.ReadWrite));
    }
}
