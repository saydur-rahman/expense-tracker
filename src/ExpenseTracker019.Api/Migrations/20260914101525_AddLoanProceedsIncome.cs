using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ExpenseTracker019.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddLoanProceedsIncome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LoanId",
                table: "Incomes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Incomes_LoanId",
                table: "Incomes",
                column: "LoanId",
                unique: true,
                filter: "[LoanId] IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_Incomes_Loans_LoanId",
                table: "Incomes",
                column: "LoanId",
                principalTable: "Loans",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Incomes_Loans_LoanId",
                table: "Incomes");

            migrationBuilder.DropIndex(
                name: "IX_Incomes_LoanId",
                table: "Incomes");

            migrationBuilder.DropColumn(
                name: "LoanId",
                table: "Incomes");
        }
    }
}
