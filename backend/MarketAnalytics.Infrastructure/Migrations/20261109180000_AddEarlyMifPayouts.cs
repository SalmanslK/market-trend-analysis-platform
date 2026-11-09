using MarketAnalytics.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <summary>
    /// Meezan Islamic Fund's payouts from 2005 to 2017, which the payout source doesn't list. Without them MIF's
    /// history before 2020 shows each payout as a fall. 2014-2017 are the amounts Al Meezan published (the 2015
    /// payout went ex on 1 July, but the first NAV after it is 7 July). 2005-2013 aren't published anywhere reachable,
    /// so they are the fund's one-day NAV drop on its July ex-date, which also includes that day's market move
    /// (usually within 1-2% of NAV). No payout was made for FY2009, FY2018 or FY2019.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261109180000_AddEarlyMifPayouts")]
    public partial class AddEarlyMifPayouts : Migration
    {
        private static readonly (string Date, decimal Amount)[] Payouts =
        {
            ("2005-07-06", 17.50m), ("2006-07-07", 17.50m), ("2007-07-10", 15.60m), ("2008-07-07", 10.00m),
            ("2010-07-08", 8.08m), ("2011-07-07", 9.27m), ("2012-07-10", 6.54m), ("2013-07-09", 14.23m),
            ("2014-05-30", 7.65m), ("2015-07-07", 2.20m), ("2016-06-24", 2.95m), ("2017-06-29", 5.00m),
        };

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            foreach (var (date, amount) in Payouts)
            {
                migrationBuilder.Sql($"""
                    INSERT INTO "FundPayouts" ("InstrumentId", "PayoutDate", "AmountPerUnit", "FetchedAt")
                    SELECT i."Id", TIMESTAMPTZ '{date} 00:00:00+00', {amount.ToString(System.Globalization.CultureInfo.InvariantCulture)}, now()
                    FROM "Instruments" i
                    WHERE i."Symbol" = 'MIF'
                      AND NOT EXISTS (SELECT 1 FROM "FundPayouts" p WHERE p."InstrumentId" = i."Id" AND p."PayoutDate" = TIMESTAMPTZ '{date} 00:00:00+00');
                    """);
            }
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            foreach (var (date, _) in Payouts)
            {
                migrationBuilder.Sql($"""
                    DELETE FROM "FundPayouts" p USING "Instruments" i
                    WHERE p."InstrumentId" = i."Id" AND i."Symbol" = 'MIF' AND p."PayoutDate" = TIMESTAMPTZ '{date} 00:00:00+00';
                    """);
            }
        }
    }
}
