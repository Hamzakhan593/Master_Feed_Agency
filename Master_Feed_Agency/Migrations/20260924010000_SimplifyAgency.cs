using Microsoft.EntityFrameworkCore.Migrations;
namespace Master_Feed_Agency.Migrations;
public partial class SimplifyAgency : Migration
{
    // Retired tables remain untouched as historical data. The application no longer maps or uses them.
    protected override void Up(MigrationBuilder migrationBuilder) { }
    protected override void Down(MigrationBuilder migrationBuilder) { }
}
