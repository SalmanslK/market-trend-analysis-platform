using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSeedDataFromModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 9);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Instruments",
                columns: new[] { "Id", "CreatedAt", "DataSourceType", "DataSourceUrl", "IsActive", "Name", "Symbol", "Type", "UpdatedAt" },
                values: new object[,]
                {
                    { 1, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(5914), 0, "https://beta-restapi.sarmaaya.pk/api/indices/price-history/KSE100?days=365", true, "KSE-100 Index", "KSE100", 0, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6131) },
                    { 2, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6398), 0, "https://beta-restapi.sarmaaya.pk/api/indices/price-history/KMI30?days=365", true, "KMI-30 Index", "KMI30", 0, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6398) },
                    { 3, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6401), 0, "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/a0aac2a4-0ec4-4718-b2e5-4580f5cd8ddd?days=365", true, "KMI Islamic Fund", "KMIF", 1, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6402) },
                    { 4, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6412), 0, "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/698bac51-81aa-4e0c-ad94-93d630e1e2a4?days=365", true, "Meezan Islamic Fund", "MIF", 1, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6412) },
                    { 5, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6415), 0, "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/f6bcc825-99fe-47f8-a09c-805f9e46697c?days=365", true, "Meezan Balanced Fund", "MBF", 1, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6415) },
                    { 6, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6463), 0, "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/7e59207d-798c-43ab-bc76-4a44d2f90a64?days=365", true, "Meezan Growth Fund", "MGF", 1, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6464) },
                    { 7, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6466), 0, "https://beta-restapi.sarmaaya.pk/api/forex/price-history/USDPKR?days=1095", true, "USD/PKR Exchange Rate", "USDPKR", 2, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6466) },
                    { 8, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6475), 1, "https://www.investing.com/currencies/xau-usd-historical-data", true, "Gold (International) - XAU/USD", "GOLDINT", 3, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6475) },
                    { 9, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6478), 0, "https://beta-restapi.sarmaaya.pk/api/commodities/price/gold?days=365", true, "Gold (Pakistan)", "GOLDPK", 3, new DateTime(2026, 1, 17, 13, 8, 42, 152, DateTimeKind.Utc).AddTicks(6478) }
                });
        }
    }
}
