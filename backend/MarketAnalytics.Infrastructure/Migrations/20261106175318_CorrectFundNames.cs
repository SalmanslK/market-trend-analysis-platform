using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CorrectFundNames : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "KSE Meezan Index Fund");

            migrationBuilder.UpdateData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 6,
                column: "Name",
                value: "Meezan Gold Fund");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 3,
                column: "Name",
                value: "KMI Islamic Fund");

            migrationBuilder.UpdateData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 6,
                column: "Name",
                value: "Meezan Growth Fund");
        }
    }
}
