using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AMRSurveillance.Migrations
{
    /// <inheritdoc />
    public partial class AddASTCardConfiguration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ManuscriptID",
                table: "Worksheet",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ASTCards",
                columns: table => new
                {
                    ASTCard = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASTCards", x => x.ASTCard);
                });

            migrationBuilder.CreateTable(
                name: "ASTCardAntibiotics",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ASTCard = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    AntibioticCode = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    DisplayOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASTCardAntibiotics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ASTCardAntibiotics_ASTCards_ASTCard",
                        column: x => x.ASTCard,
                        principalTable: "ASTCards",
                        principalColumn: "ASTCard",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ASTCardAntibiotics_Antibiotic_AntibioticCode",
                        column: x => x.AntibioticCode,
                        principalTable: "Antibiotic",
                        principalColumn: "AntibioticCode",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ASTCardOrganisms",
                columns: table => new
                {
                    ASTCard = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    WHONETID = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ASTCardOrganisms", x => new { x.ASTCard, x.WHONETID });
                    table.ForeignKey(
                        name: "FK_ASTCardOrganisms_ASTCards_ASTCard",
                        column: x => x.ASTCard,
                        principalTable: "ASTCards",
                        principalColumn: "ASTCard",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ASTCardOrganisms_Organism_WHONETID",
                        column: x => x.WHONETID,
                        principalTable: "Organism",
                        principalColumn: "WHONETID",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Worksheet_ManuscriptID",
                table: "Worksheet",
                column: "ManuscriptID");

            migrationBuilder.CreateIndex(
                name: "IX_Shipment_MRSNID",
                table: "Shipment",
                column: "MRSNID");

            migrationBuilder.CreateIndex(
                name: "IX_ASTCardAntibiotics_AntibioticCode",
                table: "ASTCardAntibiotics",
                column: "AntibioticCode");

            migrationBuilder.CreateIndex(
                name: "IX_ASTCardAntibiotics_ASTCard_AntibioticCode",
                table: "ASTCardAntibiotics",
                columns: new[] { "ASTCard", "AntibioticCode" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ASTCardAntibiotics_ASTCard_DisplayOrder",
                table: "ASTCardAntibiotics",
                columns: new[] { "ASTCard", "DisplayOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_ASTCardOrganisms_WHONETID",
                table: "ASTCardOrganisms",
                column: "WHONETID");

            migrationBuilder.Sql("""
                ;WITH SplitCards AS (
                    SELECT
                        UPPER(LTRIM(RTRIM(a.AntibioticCode))) AS AntibioticCode,
                        UPPER(LTRIM(RTRIM(s.value))) AS ASTCard
                    FROM [dbo].[Antibiotic] a
                    CROSS APPLY STRING_SPLIT(a.ASTCard, ';') s
                    WHERE NULLIF(LTRIM(RTRIM(s.value)), N'') IS NOT NULL
                ),
                DistinctCards AS (
                    SELECT DISTINCT ASTCard
                    FROM SplitCards
                )
                INSERT INTO [dbo].[ASTCards] ([ASTCard], [IsActive])
                SELECT d.ASTCard, 1
                FROM DistinctCards d
                WHERE NOT EXISTS (
                    SELECT 1 FROM [dbo].[ASTCards] c WHERE c.ASTCard = d.ASTCard
                );

                ;WITH SplitCards AS (
                    SELECT DISTINCT
                        UPPER(LTRIM(RTRIM(a.AntibioticCode))) AS AntibioticCode,
                        UPPER(LTRIM(RTRIM(s.value))) AS ASTCard
                    FROM [dbo].[Antibiotic] a
                    CROSS APPLY STRING_SPLIT(a.ASTCard, ';') s
                    WHERE NULLIF(LTRIM(RTRIM(s.value)), N'') IS NOT NULL
                ),
                OrderedPanel AS (
                    SELECT
                        ASTCard,
                        AntibioticCode,
                        ROW_NUMBER() OVER (PARTITION BY ASTCard ORDER BY AntibioticCode) AS DisplayOrder
                    FROM SplitCards
                )
                INSERT INTO [dbo].[ASTCardAntibiotics] ([ASTCard], [AntibioticCode], [DisplayOrder])
                SELECT o.ASTCard, o.AntibioticCode, o.DisplayOrder
                FROM OrderedPanel o
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM [dbo].[ASTCardAntibiotics] ca
                    WHERE ca.ASTCard = o.ASTCard AND ca.AntibioticCode = o.AntibioticCode
                );
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ASTCardAntibiotics");

            migrationBuilder.DropTable(
                name: "ASTCardOrganisms");

            migrationBuilder.DropTable(
                name: "ASTCards");

            migrationBuilder.DropIndex(
                name: "IX_Worksheet_ManuscriptID",
                table: "Worksheet");

            migrationBuilder.DropIndex(
                name: "IX_Shipment_MRSNID",
                table: "Shipment");

            migrationBuilder.DropColumn(
                name: "ManuscriptID",
                table: "Worksheet");
        }
    }
}