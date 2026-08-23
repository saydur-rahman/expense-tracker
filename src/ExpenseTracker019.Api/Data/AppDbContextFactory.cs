using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace ExpenseTracker019.Api.Data;

/// <summary>
/// Design-time only. At runtime Aspire injects the real connection string; the EF
/// tooling has no such host, so it falls back to a local SQL Server instance.
/// Override with the ExpenseDb environment variable if your local server differs.
/// </summary>
public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ExpenseDb")
            ?? "Server=.;Database=ExpenseTracker019Db;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;

        return new AppDbContext(options);
    }
}
