using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignalSnapshots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InstrumentId = table.Column<int>(type: "integer", nullable: false),
                    Date = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: false),
                    DailyAction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    DailyScore = table.Column<int>(type: "integer", nullable: false),
                    SteadyAction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: true),
                    SteadyScore = table.Column<int>(type: "integer", nullable: true),
                    NewsAction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    NewsScore = table.Column<int>(type: "integer", nullable: false),
                    NewsArticles = table.Column<int>(type: "integer", nullable: false),
                    CombinedAction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Trend = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    AheadOfCash = table.Column<bool>(type: "boolean", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalSnapshots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalSnapshots_Instruments_InstrumentId",
                        column: x => x.InstrumentId,
                        principalTable: "Instruments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignalSnapshots_InstrumentId_Date",
                table: "SignalSnapshots",
                columns: new[] { "InstrumentId", "Date" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignalSnapshots");
        }
    }
}
