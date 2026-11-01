using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SwitchGoldIntToGoldApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "DataSourceType", "DataSourceUrl" },
                values: new object[] { 2, "https://api.gold-api.com/price/XAU" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 8,
                columns: new[] { "DataSourceType", "DataSourceUrl" },
                values: new object[] { 1, "https://www.investing.com/currencies/xau-usd-historical-data" });
        }
    }
}
