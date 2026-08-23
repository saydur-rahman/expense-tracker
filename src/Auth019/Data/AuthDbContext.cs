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

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            // Must default to true at the DB level, not just via the C# initializer:
            // otherwise adding this column backfills existing rows with 0 and locks
            // every pre-existing account out.
            e.Property(u => u.IsActive).HasDefaultValue(true);
            e.HasIndex(u => u.LastLoginAtUtc);
        });
    }
}
