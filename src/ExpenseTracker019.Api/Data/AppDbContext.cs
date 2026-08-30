using ExpenseTracker019.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace ExpenseTracker019.Api.Data;

/// <summary>
/// The expense domain database. It holds no user records — users are owned by
/// Auth019 and referenced here only by the <c>UserId</c> from a token's <c>sub</c> claim,
/// so there are deliberately no foreign keys to a users table.
/// </summary>
public class AppDbContext : DbContext
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
    public DbSet<Income> Incomes => Set<Income>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<FeedbackMessage> FeedbackMessages => Set<FeedbackMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserMonthCycleSetting>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.EffectiveFromUtc });
        });

        builder.Entity<Category>(e =>
        {
            // Every list is filtered by kind, so the index leads with it rather than
            // making each query sift the other side of the ledger out.
            e.HasIndex(x => new { x.UserId, x.Kind });
            e.Property(x => x.Kind).HasDefaultValue(CategoryKind.Expense);
            // Expenses/budgets referencing an archived Category or Head must still be queryable
            // for history/reports, so those queries must call IgnoreQueryFilters() explicitly
            // instead of relying on eager-loaded navigations, which this filter would silently drop.
            e.HasQueryFilter(x => !x.IsArchived);
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
            e.Property(x => x.BudgetsInitialized).HasDefaultValue(false);
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

        builder.Entity<Feedback>(e =>
        {
            e.Property(x => x.Subject).HasMaxLength(200);
            e.Property(x => x.SubmittedByName).HasMaxLength(200);
            e.Property(x => x.SubmittedByEmail).HasMaxLength(320);
            // Admins list by status and sort by activity; users list their own.
            e.HasIndex(x => new { x.Status, x.UpdatedAtUtc });
            e.HasIndex(x => x.UserId);
        });

        builder.Entity<FeedbackMessage>(e =>
        {
            e.Property(x => x.Body).HasMaxLength(4000);
            e.Property(x => x.AuthorName).HasMaxLength(200);
            e.HasIndex(x => new { x.FeedbackId, x.CreatedAtUtc });
            // Deleting a conversation takes its messages with it — unlike categories
            // and heads, a feedback thread has no history worth preserving alone.
            e.HasOne(x => x.Feedback)
                .WithMany(f => f.Messages)
                .HasForeignKey(x => x.FeedbackId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<Expense>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.HeadId, x.ExpenseDate });
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Head)
                .WithMany(h => h.Expenses)
                .HasForeignKey(x => x.HeadId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Income>(e =>
        {
            e.Property(x => x.Amount).HasPrecision(18, 2);
            e.HasIndex(x => new { x.HeadId, x.IncomeDate });
            e.HasIndex(x => x.UserId);
            e.HasOne(x => x.Head)
                .WithMany(h => h.Incomes)
                .HasForeignKey(x => x.HeadId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
