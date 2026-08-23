using ExpenseTracker.Api.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker.Api.Data;

public class AppDbContext : IdentityDbContext<ApplicationUser, IdentityRole<Guid>, Guid>
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options)
    {
    }

    public DbSet<UserMonthCycleSetting> UserMonthCycleSettings => Set<UserMonthCycleSetting>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<Head> Heads => Set<Head>();
    public DbSet<BudgetPeriod> BudgetPeriods => Set<BudgetPeriod>();
    public DbSet<CategoryBudget> CategoryBudgets => Set<CategoryBudget>();
    public DbSet<HeadBudget> HeadBudgets => Set<HeadBudget>();
    public DbSet<Expense> Expenses => Set<Expense>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<ApplicationUser>(e =>
        {
            // Must default to true at the DB level, not just via the C# initializer:
            // otherwise adding this column backfills existing rows with 0 and locks
            // every pre-existing account out.
            e.Property(u => u.IsActive).HasDefaultValue(true);
        });

        builder.Entity<UserMonthCycleSetting>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.EffectiveFromUtc });
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Category>(e =>
        {
            e.HasIndex(x => x.UserId);
            // Expenses/budgets referencing an archived Category or Head must still be queryable
            // for history/reports, so those queries must call IgnoreQueryFilters() explicitly
            // instead of relying on eager-loaded navigations, which this filter would silently drop.
            e.HasQueryFilter(x => !x.IsArchived);
            e.HasOne(x => x.User)
                .WithMany(u => u.Categories)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Head>(e =>
        {
            e.HasIndex(x => x.CategoryId);
            // Archived heads must remain visible when their parent category is archived
            // (e.g. history views that IgnoreQueryFilters on Category still need matching Heads),
            // so the Head filter only depends on its own IsArchived flag, not the category's.
            e.HasQueryFilter(x => !x.IsArchived);
            e.HasOne(x => x.Category)
                .WithMany(c => c.Heads)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<BudgetPeriod>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.StartDate }).IsUnique();
            e.HasOne(x => x.User)
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<CategoryBudget>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.BudgetPeriodId, x.CategoryId }).IsUnique();
            e.HasOne(x => x.BudgetPeriod)
                .WithMany(p => p.CategoryBudgets)
                .HasForeignKey(x => x.BudgetPeriodId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Category)
                .WithMany(c => c.CategoryBudgets)
                .HasForeignKey(x => x.CategoryId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<HeadBudget>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.BudgetPeriodId, x.HeadId }).IsUnique();
            e.HasOne(x => x.BudgetPeriod)
                .WithMany(p => p.HeadBudgets)
                .HasForeignKey(x => x.BudgetPeriodId)
                .OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Head)
                .WithMany(h => h.HeadBudgets)
                .HasForeignKey(x => x.HeadId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Expense>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.HeadId, x.ExpenseDate });
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.User)
                .WithMany(u => u.Expenses)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Head)
                .WithMany(h => h.Expenses)
                .HasForeignKey(x => x.HeadId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<RefreshToken>(e =>
        {
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne(x => x.User)
                .WithMany(u => u.RefreshTokens)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }
}
