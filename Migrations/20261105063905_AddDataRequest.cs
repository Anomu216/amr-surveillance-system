using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AMRSurveillance.Migrations
{
    /// <inheritdoc />
    public partial class AddDataRequest : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DataRequests",
                columns: table => new
                {
                    DataRequestID = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    SelectedData = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Filters = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdentifierType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IdentifierValues = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RequestedByUserID = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReviewedByUserID = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewComments = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ExportedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataRequests", x => x.DataRequestID);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataRequests");
        }
    }
}
