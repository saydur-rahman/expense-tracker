using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Auth019.Data;

/// <summary>
/// Design-time only. At runtime Aspire injects the real connection string; the EF
/// tooling has no such host, so it falls back to a local SQL Server instance.
/// Override with the Auth019Db environment variable if your local server differs.
/// </summary>
public class AuthDbContextFactory : IDesignTimeDbContextFactory<AuthDbContext>
{
    public AuthDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("Auth019Db")
            ?? "Server=.;Database=Auth019Db;Trusted_Connection=True;TrustServerCertificate=True;";

        var options = new DbContextOptionsBuilder<AuthDbContext>()
            .UseSqlServer(connectionString)
            .UseOpenIddict()
            .Options;

        return new AuthDbContext(options);
    }
}
