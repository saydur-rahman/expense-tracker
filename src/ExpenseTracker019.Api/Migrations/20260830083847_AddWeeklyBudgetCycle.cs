using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker019.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddWeeklyBudgetCycle : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_UserId_StartDate",
                table: "BudgetPeriods");

            migrationBuilder.AddColumn<int>(
                name: "PeriodKind",
                table: "UserMonthCycleSettings",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "WeekStartsOn",
                table: "UserMonthCycleSettings",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "Kind",
                table: "BudgetPeriods",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_UserId_Kind_StartDate",
                table: "BudgetPeriods",
                columns: new[] { "UserId", "Kind", "StartDate" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_BudgetPeriods_UserId_Kind_StartDate",
                table: "BudgetPeriods");

            migrationBuilder.DropColumn(
                name: "PeriodKind",
                table: "UserMonthCycleSettings");

            migrationBuilder.DropColumn(
                name: "WeekStartsOn",
                table: "UserMonthCycleSettings");

            migrationBuilder.DropColumn(
                name: "Kind",
                table: "BudgetPeriods");

            migrationBuilder.CreateIndex(
                name: "IX_BudgetPeriods_UserId_StartDate",
                table: "BudgetPeriods",
                columns: new[] { "UserId", "StartDate" },
                unique: true);
        }
    }
}
