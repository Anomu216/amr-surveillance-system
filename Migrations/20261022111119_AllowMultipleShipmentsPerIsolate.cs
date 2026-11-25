using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AMRSurveillance.Migrations
{
    /// <inheritdoc />
    public partial class AllowMultipleShipmentsPerIsolate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipment_IsolateID",
                table: "Shipment");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SequencingDate",
                table: "Genomics",
                type: "date",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipment_IsolateID",
                table: "Shipment",
                column: "IsolateID");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Shipment_IsolateID",
                table: "Shipment");

            migrationBuilder.AlterColumn<DateTime>(
                name: "SequencingDate",
                table: "Genomics",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "date",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Shipment_IsolateID",
                table: "Shipment",
                column: "IsolateID",
                unique: true);
        }
    }
}
