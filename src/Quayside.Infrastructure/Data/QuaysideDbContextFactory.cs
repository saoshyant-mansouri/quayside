using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Quayside.Infrastructure.Data;

public sealed class QuaysideDbContextFactory : IDesignTimeDbContextFactory<QuaysideDbContext>
{
    public const string DesignTimeConnection = "Server=tcp:localhost,1433;Initial Catalog=quayside;User ID=design;Password=design;Encrypt=True;TrustServerCertificate=True";

    public QuaysideDbContext CreateDbContext(string[] args)
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__Sql") ?? DesignTimeConnection;
        var options = new DbContextOptionsBuilder<QuaysideDbContext>().UseSqlServer(connection).Options;
        return new QuaysideDbContext(options);
    }
}
