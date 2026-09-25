using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Master_Feed_Agency.Migrations
{
    /// <inheritdoc />
    public partial class module10 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ExpenseCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ExpenseCategories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Expenses",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ReferenceNo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    ExpenseCategoryId = table.Column<int>(type: "int", nullable: false),
                    Date = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    Method = table.Column<int>(type: "int", nullable: false),
                    PaidBy = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    ExternalReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    AttachmentStoredName = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    AttachmentOriginalName = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    AttachmentContentType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AttachmentSize = table.Column<long>(type: "bigint", nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    IsReversal = table.Column<bool>(type: "bit", nullable: false),
                    ReversalOfExpenseId = table.Column<long>(type: "bigint", nullable: true),
                    ReversalExpenseId = table.Column<long>(type: "bigint", nullable: true),
                    CashTransactionId = table.Column<long>(type: "bigint", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReversedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReversedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReversalReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Expenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Expenses_ExpenseCategories_ExpenseCategoryId",
                        column: x => x.ExpenseCategoryId,
                        principalTable: "ExpenseCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                table: "ExpenseCategories",
                columns: new[] { "Id", "CreatedAt", "CreatedByUserId", "IsActive", "Name", "UpdatedAt", "UpdatedByUserId" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 9, 19, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, "Fuel", null, null },
                    { 2, new DateTime(2026, 9, 19, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, "Transport", null, null },
                    { 3, new DateTime(2026, 9, 19, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, "Loading", null, null },
                    { 4, new DateTime(2026, 9, 19, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, "Salary", null, null },
                    { 5, new DateTime(2026, 9, 19, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, "Utilities", null, null },
                    { 6, new DateTime(2026, 9, 19, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, "Maintenance", null, null },
                    { 7, new DateTime(2026, 9, 19, 0, 0, 0, 0, DateTimeKind.Utc), "SYSTEM", true, "Other", null, null }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_IsActive_Name",
                table: "ExpenseCategories",
                columns: new[] { "IsActive", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_ExpenseCategories_Name",
                table: "ExpenseCategories",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_Date_Status",
                table: "Expenses",
                columns: new[] { "Date", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ExpenseCategoryId_Date",
                table: "Expenses",
                columns: new[] { "ExpenseCategoryId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ReferenceNo",
                table: "Expenses",
                column: "ReferenceNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Expenses_ReversalOfExpenseId",
                table: "Expenses",
                column: "ReversalOfExpenseId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Expenses");

            migrationBuilder.DropTable(
                name: "ExpenseCategories");
        }
    }
}
