using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AMRSurveillance.Migrations
{
    /// <inheritdoc />
    public partial class AddAntibioticMonthlyReportFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IncludeInMonthlyReport",
                table: "Antibiotic",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IncludeInMonthlyReport",
                table: "Antibiotic");
        }
    }
}
