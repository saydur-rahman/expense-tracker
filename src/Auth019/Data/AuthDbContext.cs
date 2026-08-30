using Auth019.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Auth019.Data;

/// <summary>
/// Auth019's own database — Identity tables plus OpenIddict's application,
/// authorization, scope and token tables. Entirely separate from the expense
/// database; the two services share no schema.
/// </summary>
public class AuthDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AuthDbContext(DbContextOptions<AuthDbContext> options) : base(options)
    {
    }

    /// <summary>
    /// Every table Auth019 owns lives under this schema. The two services still share
    /// no tables, but keeping them in separate schemas lets them sit in one database
    /// where hosting only offers a single free one — see docs/DEPLOY.md.
    /// </summary>
    public const string Schema = "auth";

    protected override void OnModelCreating(ModelBuilder builder)
    {
        builder.HasDefaultSchema(Schema);

        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            // Must default to true at the DB level, not just via the C# initializer:
            // otherwise adding this column backfills existing rows with 0 and locks
            // every pre-existing account out.
            e.Property(u => u.IsActive).HasDefaultValue(true);
            e.Property(u => u.Country).HasMaxLength(2);
            e.HasIndex(u => u.LastLoginAtUtc);
        });
    }
}
