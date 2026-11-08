using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLowRiskMeezanFunds : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Instruments",
                columns: new[] { "Id", "CreatedAt", "DataSourceType", "DataSourceUrl", "IsActive", "Name", "Symbol", "Type", "UpdatedAt" },
                values: new object[,]
                {
                    { 11, new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/5385b72f-44fe-4f98-bee3-0f275a05b148?days=365", true, "Meezan Rozana Amdani Fund", "MRAF", 1, new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 12, new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/b4323cd2-b729-4122-9678-7150f95c868a?days=365", true, "Meezan Islamic Income Fund", "MIIF", 1, new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { 13, new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/4021c591-4da4-48f5-ba76-5b7b6059298c?days=365", true, "Meezan Daily Income Fund - Meezan Munafa Plan I", "MMP-I", 1, new DateTime(2026, 11, 9, 0, 0, 0, 0, DateTimeKind.Utc) }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 13);
        }
    }
}
