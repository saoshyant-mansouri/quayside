using Quayside.Core.Sql;

namespace Quayside.UnitTests.Sql;

public sealed class SqlGuardTests
{
    private const int MaxRows = 200;

    private static readonly IReadOnlySet<string> Allowed = new HashSet<string>(StringComparer.Ordinal)
    {
        "Bookings", "Containers", "Customers", "Vessels", "Voyages", "Ports", "PortCalls", "ContainerMovements", "ReeferReadings"
    };

    private static SqlValidation Check(string sql) => SqlGuard.Validate(sql, Allowed, MaxRows);

    private static void AssertRejected(string sql, string reasonFragment)
    {
        var result = Check(sql);
        Assert.False(result.Accepted, $"Expected rejection of: {sql}");
        Assert.NotNull(result.Reason);
        Assert.Contains(reasonFragment, result.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.TablesTouched);
    }

    [Fact]
    public void MultiJoinSelectAcrossRealTablesIsAccepted()
    {
        const string sql = """
            SELECT TOP (50) b.BookingNumber, c.ContainerNumber, cu.CustomerName, v.VesselName, p.PortName
            FROM ops.Bookings AS b
            JOIN ops.Customers AS cu ON cu.CustomerId = b.CustomerId
            JOIN ops.ContainerMovements AS m ON m.BookingId = b.BookingId
            JOIN ops.Containers AS c ON c.ContainerId = m.ContainerId
            JOIN ops.Voyages AS vo ON vo.VoyageId = m.VoyageId
            JOIN ops.Vessels AS v ON v.VesselId = vo.VesselId
            LEFT JOIN ops.Ports AS p ON p.PortId = b.OriginPortId
            WHERE m.OccurredAtUtc >= '2026-01-01'
            ORDER BY m.OccurredAtUtc DESC
            """;

        var result = Check(sql);

        Assert.True(result.Accepted, result.Reason);
        Assert.Null(result.Reason);
        Assert.Equal(["Bookings", "ContainerMovements", "Containers", "Customers", "Ports", "Vessels", "Voyages"], result.TablesTouched);
    }

    [Fact]
    public void AliasesResolveToTheUnderlyingTables()
    {
        var result = Check("SELECT TOP (10) b.BookingNumber FROM ops.Bookings b JOIN ops.Containers c ON c.ContainerId = b.BookingId");

        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(["Bookings", "Containers"], result.TablesTouched);
    }

    [Fact]
    public void AliasNamedLikeADisallowedTableDoesNotChangeTheVerdict()
    {
        var result = Check("SELECT TOP (10) Invoices.BookingNumber FROM ops.Bookings AS Invoices");

        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(["Bookings"], result.TablesTouched);
    }

    [Fact]
    public void DisallowedTableBehindAnAliasIsStillRejected()
    {
        AssertRejected("SELECT TOP (10) b.InvoiceId FROM ops.Invoices AS b", "ops.Invoices");
    }

    [Fact]
    public void CteOverAnAllowedTableIsAcceptedAndTheCteIsNotCountedAsATable()
    {
        const string sql = """
            WITH recent AS (SELECT BookingId, CustomerId FROM ops.Bookings)
            SELECT TOP (10) r.BookingId, cu.CustomerName
            FROM recent r JOIN ops.Customers cu ON cu.CustomerId = r.CustomerId
            """;

        var result = Check(sql);

        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(["Bookings", "Customers"], result.TablesTouched);
    }

    [Fact]
    public void CteOverADisallowedTableIsRejected()
    {
        AssertRejected("WITH x AS (SELECT InvoiceId FROM ops.Invoices) SELECT TOP (10) * FROM x", "ops.Invoices");
    }

    [Fact]
    public void CteNamedLikeADisallowedTableCannotSmuggleItIn()
    {
        AssertRejected("WITH Invoices AS (SELECT InvoiceId FROM ops.Invoices) SELECT TOP (10) * FROM Invoices", "ops.Invoices");
    }

    [Fact]
    public void CteReferenceBeforeItsDefinitionResolvesToARealTableAndIsRejected()
    {
        const string sql = """
            WITH first_cte AS (SELECT * FROM later_cte),
                 later_cte AS (SELECT BookingId FROM ops.Bookings)
            SELECT TOP (10) * FROM first_cte
            """;

        AssertRejected(sql, "later_cte");
    }

    [Fact]
    public void LaterCteMayReadAnEarlierOne()
    {
        const string sql = """
            WITH a AS (SELECT BookingId FROM ops.Bookings),
                 b AS (SELECT BookingId FROM a)
            SELECT TOP (10) * FROM b
            """;

        var result = Check(sql);

        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(["Bookings"], result.TablesTouched);
    }

    [Fact]
    public void RecursiveCteIsAccepted()
    {
        const string sql = """
            WITH walk AS (
                SELECT BookingId, 0 AS Depth FROM ops.Bookings
                UNION ALL
                SELECT BookingId, Depth + 1 FROM walk WHERE Depth < 3
            )
            SELECT TOP (10) * FROM walk
            """;

        var result = Check(sql);

        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(["Bookings"], result.TablesTouched);
    }

    [Fact]
    public void SubqueryReferencingADisallowedTableIsRejected()
    {
        AssertRejected("SELECT TOP (10) BookingNumber FROM ops.Bookings WHERE CustomerId IN (SELECT CustomerId FROM ops.Invoices)", "ops.Invoices");
    }

    [Fact]
    public void ScalarSubqueryAndDerivedTableReferencingDisallowedTablesAreRejected()
    {
        AssertRejected("SELECT TOP (10) (SELECT COUNT(*) FROM ops.Invoices) AS n FROM ops.Bookings", "ops.Invoices");
        AssertRejected("SELECT TOP (10) * FROM (SELECT * FROM ops.Invoices) AS d", "ops.Invoices");
    }

    [Fact]
    public void SubqueryOverAllowedTablesIsAcceptedAndTouchedTablesIncludeIt()
    {
        var result = Check("SELECT TOP (10) BookingNumber FROM ops.Bookings WHERE CustomerId IN (SELECT CustomerId FROM ops.Customers)");

        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(["Bookings", "Customers"], result.TablesTouched);
    }

    [Fact]
    public void TableOutsideTheAllowListIsRejected()
    {
        AssertRejected("SELECT TOP (10) * FROM ops.Invoices", "not in the allowed set");
    }

    [Theory]
    [InlineData("SELECT TOP (10) * FROM Bookings")]
    [InlineData("SELECT TOP (10) * FROM dbo.Bookings")]
    [InlineData("SELECT TOP (10) * FROM sys.objects")]
    [InlineData("SELECT TOP (10) * FROM INFORMATION_SCHEMA.TABLES")]
    [InlineData("SELECT TOP (10) * FROM #scratch")]
    public void TablesOutsideSchemaOpsOrUnqualifiedAreRejected(string sql)
    {
        Assert.False(Check(sql).Accepted);
    }

    [Fact]
    public void CrossDatabaseNameIsRejected()
    {
        AssertRejected("SELECT TOP (10) * FROM master.ops.Bookings", "Cross-database");
    }

    [Fact]
    public void LinkedServerFourPartNameIsRejected()
    {
        AssertRejected("SELECT TOP (10) * FROM RemoteServer.Quayside.ops.Bookings", "Linked-server");
    }

    [Fact]
    public void OpenRowsetIsRejected()
    {
        AssertRejected("SELECT TOP (10) * FROM OPENROWSET('SQLNCLI', 'Server=x;Trusted_Connection=yes;', 'SELECT 1 AS a') AS r", "OPENROWSET");
    }

    [Fact]
    public void OpenRowsetBulkIsRejected()
    {
        AssertRejected("SELECT TOP (10) * FROM OPENROWSET(BULK 'C:\\data.csv', SINGLE_CLOB) AS r", "OPENROWSET");
    }

    [Fact]
    public void OpenQueryIsRejected()
    {
        AssertRejected("SELECT TOP (10) * FROM OPENQUERY(RemoteServer, 'SELECT 1 AS a')", "OPENQUERY");
    }

    [Fact]
    public void OpenDataSourceIsRejected()
    {
        AssertRejected("SELECT TOP (10) * FROM OPENDATASOURCE('SQLNCLI', 'Data Source=x;').Quayside.ops.Bookings", "OPENDATASOURCE");
    }

    [Fact]
    public void OpenJsonAndTableValuedFunctionsAreRejected()
    {
        Assert.False(Check("SELECT TOP (10) * FROM OPENJSON('[1]')").Accepted);
        Assert.False(Check("SELECT TOP (10) * FROM ops.GetBookings(1)").Accepted);
        Assert.False(Check("SELECT TOP (10) * FROM sys.dm_exec_sessions").Accepted);
    }

    [Fact]
    public void CrossApplyOfAFunctionIsRejected()
    {
        Assert.False(Check("SELECT TOP (10) b.BookingId FROM ops.Bookings b CROSS APPLY sys.fn_helpcollations() f").Accepted);
    }

    [Fact]
    public void SelectIntoIsRejected()
    {
        AssertRejected("SELECT TOP (10) * INTO ops.Stolen FROM ops.Bookings", "INTO");
    }

    [Fact]
    public void SelectIntoTempTableIsRejected()
    {
        AssertRejected("SELECT TOP (10) * INTO #t FROM ops.Bookings", "INTO");
    }

    [Theory]
    [InlineData("INSERT INTO ops.Bookings (BookingNumber) VALUES ('x')", "INSERT")]
    [InlineData("UPDATE ops.Bookings SET BookingNumber = 'x'", "UPDATE")]
    [InlineData("DELETE FROM ops.Bookings", "DELETE")]
    [InlineData("MERGE ops.Bookings AS t USING ops.Customers AS s ON t.CustomerId = s.CustomerId WHEN MATCHED THEN DELETE;", "MERGE")]
    [InlineData("TRUNCATE TABLE ops.Bookings", "TRUNCATE")]
    [InlineData("DROP TABLE ops.Bookings", "DROP")]
    [InlineData("CREATE TABLE ops.X (Id int)", "CREATE")]
    [InlineData("ALTER TABLE ops.Bookings ADD Extra int", "ALTER")]
    [InlineData("GRANT SELECT ON ops.Bookings TO public", "GRANT")]
    [InlineData("EXEC sp_who", "EXEC")]
    [InlineData("EXEC xp_cmdshell 'dir'", "EXEC")]
    [InlineData("EXEC('SELECT 1')", "EXEC")]
    [InlineData("EXECUTE sp_executesql N'SELECT 1'", "EXEC")]
    [InlineData("WAITFOR DELAY '00:00:30'", "WAITFOR")]
    [InlineData("DECLARE c CURSOR FOR SELECT BookingId FROM ops.Bookings", "Cursors")]
    [InlineData("DECLARE @x int = 1", "Variables")]
    [InlineData("SET NOCOUNT ON", "SET statements")]
    public void NonSelectStatementsAreRejected(string sql, string reasonFragment)
    {
        AssertRejected(sql, reasonFragment);
    }

    [Fact]
    public void WaitForInsideABatchWithASelectIsRejected()
    {
        AssertRejected("SELECT TOP (1) BookingId FROM ops.Bookings; WAITFOR DELAY '00:01:00'", "WAITFOR");
    }

    [Fact]
    public void DynamicSqlAfterASelectIsRejected()
    {
        AssertRejected("SELECT TOP (1) BookingId FROM ops.Bookings; EXEC('DROP TABLE ops.Bookings')", "EXEC");
    }

    [Fact]
    public void BatchSmugglingIsRejected()
    {
        AssertRejected("SELECT 1; DROP TABLE ops.Bookings", "Exactly one statement");
    }

    [Fact]
    public void TwoSelectsAreRejected()
    {
        AssertRejected("SELECT TOP (1) BookingId FROM ops.Bookings; SELECT TOP (1) ContainerId FROM ops.Containers", "Exactly one statement");
    }

    [Fact]
    public void TrailingSemicolonIsHarmless()
    {
        var result = Check("SELECT TOP (1) BookingId FROM ops.Bookings;");

        Assert.True(result.Accepted, result.Reason);
    }

    [Fact]
    public void GoBatchSeparatorIsRejected()
    {
        Assert.False(Check("SELECT TOP (1) BookingId FROM ops.Bookings\nGO\nDROP TABLE ops.Bookings").Accepted);
    }

    [Theory]
    [InlineData("SELECT TOP (10) * FROM ops.Bookings -- ")]
    [InlineData("SELECT TOP (10) * FROM ops.Bookings -- ; DROP TABLE ops.Bookings")]
    [InlineData("SELECT TOP (10) * FROM ops.Bookings /* harmless */")]
    [InlineData("SELECT TOP (10) /* DROP */ * FROM ops.Bookings")]
    [InlineData("SELECT TOP (10) * FROM ops.Bookings\n-- x\n; DROP TABLE ops.Bookings")]
    public void CommentsAreRejectedSoNothingCanHideInThem(string sql)
    {
        AssertRejected(sql, "Comments");
    }

    [Fact]
    public void UnterminatedCommentIsAParseFailure()
    {
        Assert.False(Check("SELECT TOP (10) * FROM ops.Bookings /* ; DROP TABLE ops.Bookings").Accepted);
    }

    [Fact]
    public void DashesInsideAStringLiteralAreNotComments()
    {
        var result = Check("SELECT TOP (10) * FROM ops.Bookings WHERE BookingNumber = 'a -- b /* c */'");

        Assert.True(result.Accepted, result.Reason);
    }

    [Fact]
    public void ParseErrorsAreRejectedWithTheParserMessage()
    {
        AssertRejected("SELEC TOP (10) * FROM ops.Bookings", "does not parse");
        AssertRejected("SELECT TOP (10) * FROM", "does not parse");
        AssertRejected("SELECT TOP (10) * FROM ops.Bookings WHERE (", "does not parse");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   \n\t ")]
    public void EmptySqlIsRejected(string sql)
    {
        AssertRejected(sql, "empty");
    }

    [Fact]
    public void ProseThatIsNotSqlIsRejected()
    {
        Assert.False(Check("CANNOT_ANSWER").Accepted);
        Assert.False(Check("I could not find a way to answer that.").Accepted);
    }

    [Fact]
    public void MissingTopIsRejected()
    {
        AssertRejected("SELECT * FROM ops.Bookings", "TOP");
    }

    [Fact]
    public void TopAboveMaxRowsIsRejected()
    {
        AssertRejected("SELECT TOP (10000) * FROM ops.Bookings", "above the maximum");
    }

    [Theory]
    [InlineData("SELECT TOP (200) * FROM ops.Bookings", true)]
    [InlineData("SELECT TOP (201) * FROM ops.Bookings", false)]
    [InlineData("SELECT TOP 5 * FROM ops.Bookings", true)]
    [InlineData("SELECT TOP (99999999999999999999) * FROM ops.Bookings", false)]
    public void TopBoundaryIsExact(string sql, bool accepted)
    {
        Assert.Equal(accepted, Check(sql).Accepted);
    }

    [Theory]
    [InlineData("SELECT TOP (10) PERCENT * FROM ops.Bookings", "PERCENT")]
    [InlineData("SELECT TOP (10) WITH TIES * FROM ops.Bookings ORDER BY BookingId", "WITH TIES")]
    [InlineData("SELECT TOP (SELECT COUNT(*) FROM ops.Bookings) * FROM ops.Bookings", "integer literal")]
    [InlineData("SELECT TOP (5 + 5) * FROM ops.Bookings", "integer literal")]
    public void TopThatCouldReturnMoreThanNRowsIsRejected(string sql, string reasonFragment)
    {
        AssertRejected(sql, reasonFragment);
    }

    [Fact]
    public void TopOnlyInASubqueryDoesNotBoundTheOutermostQuery()
    {
        AssertRejected("SELECT * FROM (SELECT TOP (10) * FROM ops.Bookings) AS d", "TOP");
    }

    [Fact]
    public void UnionAtTheTopLevelIsRejectedButAcceptedInsideADerivedTable()
    {
        AssertRejected("SELECT TOP (10) BookingId FROM ops.Bookings UNION ALL SELECT TOP (10) ContainerId FROM ops.Containers", "outermost");

        var wrapped = Check("SELECT TOP (20) u.Id FROM (SELECT BookingId AS Id FROM ops.Bookings UNION ALL SELECT ContainerId FROM ops.Containers) AS u");
        Assert.True(wrapped.Accepted, wrapped.Reason);
        Assert.Equal(["Bookings", "Containers"], wrapped.TablesTouched);
    }

    [Fact]
    public void ParenthesisedWholeQueryIsUnwrapped()
    {
        var result = Check("(SELECT TOP (10) * FROM ops.Bookings)");

        Assert.True(result.Accepted, result.Reason);
    }

    [Theory]
    [InlineData("select top (10) * from ops.bookings")]
    [InlineData("SELECT TOP (10) * FROM OPS.BOOKINGS")]
    [InlineData("  SELECT   TOP   (  10  )   *   FROM   ops.Bookings  ")]
    [InlineData("SELECT\nTOP (10)\n*\nFROM\n\tops.Bookings")]
    [InlineData("SELECT TOP (10) * FROM [ops].[Bookings]")]
    [InlineData("SeLeCt ToP (10) * FrOm ops.BoOkInGs")]
    public void CaseWhitespaceAndQuotingDoNotChangeTheVerdict(string sql)
    {
        var result = Check(sql);

        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(["Bookings"], result.TablesTouched);
    }

    [Theory]
    [InlineData("drop table ops.Bookings")]
    [InlineData("  DrOp   TaBlE   ops.Bookings  ")]
    [InlineData("select * from ops.invoices")]
    [InlineData("SELECT\nTOP (10000)\n*\nFROM ops.Bookings")]
    public void CaseAndWhitespaceVariantsOfBadSqlAreStillRejected(string sql)
    {
        Assert.False(Check(sql).Accepted);
    }

    [Fact]
    public void AllowListEntriesMayCarryTheSchemaPrefix()
    {
        var prefixed = new HashSet<string> { "ops.Bookings" };

        var result = SqlGuard.Validate("SELECT TOP (5) * FROM ops.Bookings", prefixed, MaxRows);

        Assert.True(result.Accepted, result.Reason);
        Assert.Equal(["Bookings"], result.TablesTouched);
    }

    [Fact]
    public void TablesTouchedIsDistinctAndSorted()
    {
        var result = Check("SELECT TOP (5) a.BookingId FROM ops.Bookings a JOIN ops.Bookings b ON a.BookingId = b.BookingId JOIN ops.Containers c ON 1 = 1");

        Assert.Equal(["Bookings", "Containers"], result.TablesTouched);
    }

    [Fact]
    public void SelectWithoutFromIsAcceptedWhenItHasTop()
    {
        var result = Check("SELECT TOP (1) 1 AS One");

        Assert.True(result.Accepted, result.Reason);
        Assert.Empty(result.TablesTouched);
    }

    [Theory]
    [InlineData("SELECT TOP (1) @x = BookingId FROM ops.Bookings")]
    [InlineData("SELECT TOP (1) NEXT VALUE FOR ops.BookingSeq")]
    [InlineData("SELECT TOP (1) dbo.Evil(BookingId) FROM ops.Bookings")]
    [InlineData("SELECT TOP (1) BookingId FROM ops.Bookings WITH (TABLOCKX)")]
    [InlineData("SELECT TOP (1) BookingId FROM ops.Bookings WITH (UPDLOCK)")]
    public void SideEffectsAndLocksInsideASelectAreRejected(string sql)
    {
        Assert.False(Check(sql).Accepted);
    }

    [Fact]
    public void NoLockHintIsPermitted()
    {
        var result = Check("SELECT TOP (1) BookingId FROM ops.Bookings WITH (NOLOCK)");

        Assert.True(result.Accepted, result.Reason);
    }

    [Fact]
    public void DeeplyNestedParenthesesAreRejectedBeforeTheParserRecurses()
    {
        var sql = "SELECT TOP (1) " + new string('(', 500) + "1" + new string(')', 500) + " FROM ops.Bookings";

        AssertRejected(sql, "nested");
    }

    [Fact]
    public void OversizedInputIsRejected()
    {
        var sql = "SELECT TOP (1) '" + new string('a', SqlGuard.MaxLength) + "' FROM ops.Bookings";

        AssertRejected(sql, "longer than");
    }

    [Fact]
    public void RejectionIsAResultAndNotAnException()
    {
        var result = Check("DROP DATABASE Quayside");

        Assert.False(result.Accepted);
        Assert.False(string.IsNullOrWhiteSpace(result.Reason));
    }

    [Fact]
    public void NullAllowListAndNonPositiveMaxRowsAreCallerBugs()
    {
        Assert.Throws<ArgumentNullException>(() => SqlGuard.Validate("SELECT TOP (1) 1", null!, MaxRows));
        Assert.Throws<ArgumentOutOfRangeException>(() => SqlGuard.Validate("SELECT TOP (1) 1", Allowed, 0));
    }

    [Fact]
    public void NullSqlIsRejected()
    {
        Assert.False(SqlGuard.Validate(null!, Allowed, MaxRows).Accepted);
    }
}
