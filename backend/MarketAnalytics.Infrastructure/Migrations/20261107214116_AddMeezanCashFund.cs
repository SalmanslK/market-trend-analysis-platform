using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMeezanCashFund : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Instruments",
                columns: new[] { "Id", "CreatedAt", "DataSourceType", "DataSourceUrl", "IsActive", "Name", "Symbol", "Type", "UpdatedAt" },
                values: new object[] { 10, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc), 0, "https://beta-restapi.sarmaaya.pk/api/mutual-funds/nav-history/a0674272-8a64-4fb8-b708-4e60a1e7f135?days=365", true, "Meezan Cash Fund", "MCF", 1, new DateTime(2026, 11, 8, 0, 0, 0, 0, DateTimeKind.Utc) });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "Instruments",
                keyColumn: "Id",
                keyValue: 10);
        }
    }
}
