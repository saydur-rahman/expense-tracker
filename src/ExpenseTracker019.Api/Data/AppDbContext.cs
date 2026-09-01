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
    public DbSet<Loan> Loans => Set<Loan>();
    public DbSet<LoanHead> LoanHeads => Set<LoanHead>();
    public DbSet<Investment> Investments => Set<Investment>();
    public DbSet<InvestmentHead> InvestmentHeads => Set<InvestmentHead>();
    public DbSet<Feedback> Feedbacks => Set<Feedback>();
    public DbSet<FeedbackMessage> FeedbackMessages => Set<FeedbackMessage>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.Entity<UserMonthCycleSetting>(e =>
        {
            e.HasIndex(x => new { x.UserId, x.EffectiveFromUtc });

            // The column defaults exist to backfill rows written before these columns did;
            // ValueGeneratedNever stops EF treating them as store-generated. Without it EF
            // sends DEFAULT for any value that equals the CLR default, so a user choosing
            // Sunday (DayOfWeek 0) would silently be given the column default of Monday.
            e.Property(x => x.PeriodKind).HasDefaultValue(PeriodKind.Month).ValueGeneratedNever();
            e.Property(x => x.WeekStartsOn).HasDefaultValue(DayOfWeek.Monday).ValueGeneratedNever();
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
            // Kind is part of the key: a user who switches cadence can legitimately have a
            // week and a month starting on the same day, and they must stay separate rows.
            e.HasIndex(x => new { x.UserId, x.Kind, x.StartDate }).IsUnique();
            // Same reasoning as UserMonthCycleSetting above: the default backfills old rows,
            // ValueGeneratedNever keeps EF writing the value we actually chose.
            e.Property(x => x.Kind).HasDefaultValue(PeriodKind.Month).ValueGeneratedNever();
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

        builder.Entity<Loan>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Lender).HasMaxLength(120);
            e.Property(x => x.Remark).HasMaxLength(1000);
            e.Property(x => x.AmountTaken).HasPrecision(18, 2);
            e.HasIndex(x => x.UserId);
        });

        builder.Entity<LoanHead>(e =>
        {
            // The rule that makes "every expense on the head repays the loan" safe: one
            // head, one loan. Two loans sharing a head would both count the same payment.
            e.HasIndex(x => x.HeadId).IsUnique();

            e.HasOne(x => x.Loan)
                .WithMany(l => l.Heads)
                .HasForeignKey(x => x.LoanId)
                .OnDelete(DeleteBehavior.Cascade);

            // Restrict, like every other reference to a head: heads are archived, never
            // deleted, and a loan's history must survive that.
            e.HasOne(x => x.Head)
                .WithMany(h => h.LoanHeads)
                .HasForeignKey(x => x.HeadId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<Investment>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(120).IsRequired();
            e.Property(x => x.Remark).HasMaxLength(1000);
            e.HasIndex(x => x.UserId);
        });

        builder.Entity<InvestmentHead>(e =>
        {
            e.HasIndex(x => x.HeadId).IsUnique();

            // No HasDefaultValue on Direction, and so no need for ValueGeneratedNever:
            // there is no older data to backfill, and a store default on an enum is the
            // trap that silently rewrote DayOfWeek.Sunday as Monday (see the comment on
            // UserMonthCycleSetting above).
            e.HasOne(x => x.Investment)
                .WithMany(i => i.Heads)
                .HasForeignKey(x => x.InvestmentId)
                .OnDelete(DeleteBehavior.Cascade);

            e.HasOne(x => x.Head)
                .WithMany(h => h.InvestmentHeads)
                .HasForeignKey(x => x.HeadId)
                .OnDelete(DeleteBehavior.Restrict);
        });
    }
}
