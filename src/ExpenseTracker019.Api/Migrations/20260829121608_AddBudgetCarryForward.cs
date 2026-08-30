using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker019.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddBudgetCarryForward : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "BudgetsInitialized",
                table: "BudgetPeriods",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // A period that already holds budgets was settled by the user before this
            // column existed. Marking it true keeps carry-forward off it for good.
            migrationBuilder.Sql(@"
                UPDATE p
                SET p.BudgetsInitialized = 1
                FROM BudgetPeriods p
                WHERE EXISTS (SELECT 1 FROM CategoryBudgets cb WHERE cb.BudgetPeriodId = p.Id);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BudgetsInitialized",
                table: "BudgetPeriods");
        }
    }
}
