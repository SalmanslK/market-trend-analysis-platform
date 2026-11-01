using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SwitchGoldIntToCurrencyApi : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 8,
                column: "DataSourceUrl",
                value: "https://cdn.jsdelivr.net/npm/@fawazahmed0/currency-api@{date}/v1/currencies/xau.json");

            // Drop GOLDINT prices from the previous source so the worker backfills a consistent year of history
            // (the incremental update only fetches dates after the latest stored price)
            migrationBuilder.Sql("DELETE FROM \"PricePoints\" WHERE \"InstrumentId\" = 8;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 8,
                column: "DataSourceUrl",
                value: "https://api.gold-api.com/price/XAU");
        }
    }
}
