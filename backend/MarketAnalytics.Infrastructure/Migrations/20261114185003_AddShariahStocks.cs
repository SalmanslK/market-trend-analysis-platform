using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddShariahStocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Instruments",
                columns: new[] { "Id", "CreatedAt", "DataSourceType", "DataSourceUrl", "IsActive", "LowRiskKind", "Name", "Symbol", "Type", "UpdatedAt" },
                values: new object[,]
                {
                    { 14, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/stocks/price-history/GHNI?days=365", true, null, "Ghandhara Industries Limited", "GHNI", 4, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 15, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/stocks/price-history/SYS?days=365", true, null, "Systems Limited", "SYS", 4, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 16, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/stocks/price-history/SAZEW?days=365", true, null, "Sazgar Engineering Works Limited", "SAZEW", 4, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 17, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/stocks/price-history/SRVI?days=365", true, null, "Service Industries Limited", "SRVI", 4, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 18, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/stocks/price-history/PIOC?days=365", true, null, "Pioneer Cement Limited", "PIOC", 4, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 19, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/stocks/price-history/JVDC?days=365", true, null, "Javedan Corporation Limited", "JVDC", 4, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 20, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/stocks/price-history/MARI?days=365", true, null, "Mari Energies Limited", "MARI", 4, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 21, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/stocks/price-history/FATIMA?days=365", true, null, "Fatima Fertilizer Company Limited", "FATIMA", 4, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 22, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/stocks/price-history/MEBL?days=365", true, null, "Meezan Bank Limited", "MEBL", 4, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 23, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/stocks/price-history/DCR?days=365", true, null, "Dolmen City REIT", "DCR", 4, new DateTime(2026, 11, 15, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 15);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 16);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 17);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 18);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 19);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 20);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 21);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 22);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 23);
        }
    }
}
