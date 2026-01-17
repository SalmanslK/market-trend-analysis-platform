using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Instruments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Symbol = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    DataSourceUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    DataSourceType = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Instruments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DataUpdateLogs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    LastUpdateDate = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecordsFetched = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ErrorMessage = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DataUpdateLogs", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DataUpdateLogs_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PricePoints",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    DateTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PricePoints", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PricePoints_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

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

            migrationBuilder.CreateIndex(
                name: "IX_DataUpdateLogs_CreatedAt",
                table: "DataUpdateLogs",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_DataUpdateLogs_InstrumentId",
                table: "DataUpdateLogs",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_Instruments_Symbol",
                table: "Instruments",
                column: "Symbol",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PricePoints_DateTime",
                table: "PricePoints",
                column: "DateTime");

            migrationBuilder.CreateIndex(
                name: "IX_PricePoints_InstrumentId",
                table: "PricePoints",
                column: "InstrumentId");

            migrationBuilder.CreateIndex(
                name: "IX_PricePoints_InstrumentId_DateTime",
                table: "PricePoints",
                columns: new[] { "InstrumentId", "DateTime" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DataUpdateLogs");

            migrationBuilder.DropTable(
                name: "PricePoints");

            migrationBuilder.DropTable(
                name: "Instruments");
        }
    }
}
