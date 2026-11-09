using MarketAnalytics.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace MarketAnalytics.Infrastructure.Migrations
{
    /// <summary>
    /// Meezan Cash Fund's payouts from 2010 to 2017, which the payout source doesn't list. Without them the fund's
    /// price sat near Rs 50 for years and its return looked like zero. A cash fund's NAV barely moves day to day, so
    /// each payout is the NAV's one-day drop (quarterly in 2010, monthly from 2011 to mid-2014, then yearly). Where a
    /// drop spans a gap in the price data, it is a little smaller than the payout, by the interest earned in the gap.
    /// </summary>
    [DbContext(typeof(ApplicationDbContext))]
    [Migration("20261109200000_AddEarlyMcfPayouts")]
    public partial class AddEarlyMcfPayouts : Migration
    {
        private static readonly (string Date, decimal Amount)[] Payouts =
        {
            ("2010-04-05", 1.44m), ("2010-07-08", 1.59m), ("2010-10-01", 1.20m), ("2011-04-08", 0.86m), 
            ("2011-05-24", 0.80m), ("2011-06-29", 0.55m), ("2011-08-24", 0.83m), ("2011-09-24", 0.42m), 
            ("2011-10-25", 0.36m), ("2011-11-24", 0.47m), ("2011-12-23", 0.37m), ("2012-01-24", 0.38m), 
            ("2012-02-24", 0.41m), ("2012-03-22", 0.36m), ("2012-04-25", 0.33m), ("2012-05-25", 0.41m), 
            ("2012-06-08", 0.18m), ("2012-06-25", 0.25m), ("2012-07-24", 0.24m), ("2012-08-24", 0.41m), 
            ("2012-09-24", 0.33m), ("2012-10-24", 0.40m), ("2012-11-26", 0.32m), ("2012-12-24", 0.28m), 
            ("2013-04-25", 0.26m), ("2013-05-27", 0.26m), ("2013-06-25", 0.28m), ("2013-07-25", 0.24m), 
            ("2013-08-26", 0.26m), ("2013-09-24", 0.27m), ("2013-10-25", 0.31m), ("2013-11-25", 0.23m), 
            ("2013-12-24", 0.29m), ("2014-01-24", 0.29m), ("2014-02-24", 0.27m), ("2014-03-24", 0.27m), 
            ("2014-04-24", 0.25m), ("2014-05-23", 0.24m), ("2014-06-24", 0.25m), ("2015-07-08", 2.97m), 
            ("2016-06-24", 2.23m), ("2017-06-29", 2.68m)
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
                    WHERE i."Symbol" = 'MCF'
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
                    WHERE p."InstrumentId" = i."Id" AND i."Symbol" = 'MCF' AND p."PayoutDate" = TIMESTAMPTZ '{date} 00:00:00+00';
                    """);
            }
        }
    }
}
