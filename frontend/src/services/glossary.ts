/**
 * Plain-language explanations shared by the ⓘ tooltips and the Guide page.
 * Written for someone with no finance background: what it is, and how to read it.
 */
export interface GlossaryEntry {
  title: string
  /** One or two sentences: what it is */
  what: string
  /** How to read it / what it means for a decision */
  howToRead: string
}

export const glossary = {
  price: {
    title: 'Price',
    what: 'The latest closing value: index points for KSE-100 and KMI-30, the fund\'s value per unit (NAV) for Meezan funds, rupees per US dollar for USD/PKR, US dollars per ounce for international gold, and rupees per tola for Pakistan gold.',
    howToRead: 'Compare it with earlier prices (the % changes) rather than reading it on its own.',
  },
  change: {
    title: '% change',
    what: 'How much the price has gone up (green) or down (red) compared with an earlier date.',
    howToRead: '1D = since the previous trading day, 1W = a week ago, 1M = a month ago, 3M = three months ago, YTD = since the start of this year, 1Y = a year ago.',
  },
  longTermTrend: {
    title: 'Long-term trend',
    what: 'Up means prices over the last 50 days have averaged higher than over the last 200 days. Down means they have averaged lower. This is called the golden cross rule.',
    howToRead: 'It tells you the direction so far, not what happens next, and it reacts slowly: it can stay Up for weeks after prices start falling. It looks only at past prices, so it knows nothing about company results, news, dividends or your own goals.',
  },
  newsSignal: {
    title: 'News signal',
    what: 'Buy or Sell from the tone of the last 7 days of news about this instrument, from -100 to +100. +20 or more is Buy, +60 or more Strong Buy, -20 or less Sell, -60 or less Strong Sell. With fewer than 3 stories it stays on Hold.',
    howToRead: 'Sentiment is read automatically from headline wording, so complicated headlines can be misread. Many headlines report moves that have already happened, so news can lag the price.',
  },
  steadySignal: {
    title: 'Steady or daily technical signal',
    what: 'Daily: today\'s five indicator votes, which can flip from Buy to Sell from one day to the next. Steady: the same daily score averaged over the last 20 trading days, so one odd day doesn\'t change the call.',
    howToRead: 'In testing over each instrument\'s history, at 0.5% per trade, the steady signal earned about +17% a year with 2 trades a year, the daily one about +7% with 19 trades; simply holding earned about +20%. Steady is the default; the combined signal, alerts and track record follow your choice.',
  },
  combinedSignal: {
    title: 'Combined signal',
    what: 'Buy or Sell only when the technical and news signals clearly point the same way (each at least +20, or each at most -20). Its score is their average. Otherwise it says Hold.',
    howToRead: 'It trades less than either signal alone and had smaller falls in testing, but it earned less than simply holding. On Hold, the idea is to stay as you are: it isn\'t a reason to buy or to sell. The Track Record page has the results.',
  },
  signal: {
    title: 'Technical signal',
    what: 'A summary of five common chart indicators. Each one votes +1 (points up), -1 (points down) or 0 (no view). The total gives Strong Buy, Buy, Hold, Sell or Strong Sell.',
    howToRead: 'It is a quick read of what the indicators show right now. It looks only at past prices, so it knows nothing about company results, news, dividends or your own goals. The Track Record page shows how it did in the past.',
  },
  score: {
    title: 'Signal score',
    what: 'All three signals use the same scale, from -100 to +100. For the technical signal it is the indicator votes as a share of those checked: 2 more votes up than down, out of 5, is +40.',
    howToRead: '+60 or more is Strong Buy, +20 or more Buy, -20 or less Sell, -60 or less Strong Sell, and anything between is Hold. The closer to zero, the more mixed the picture.',
  },
  trend: {
    title: 'Price vs its averages',
    what: 'A moving average is the average price over the last N days. Comparing today\'s price with its 50-day and 200-day averages shows whether it is above or below where it has recently been.',
    howToRead: 'This reacts faster than the long-term trend label, so the two can disagree: after a dip, the price can already be back above its averages while the label still says Down.',
  },
  sma20: {
    title: 'SMA-20 (blue line)',
    what: 'The average of the last 20 prices. It smooths out daily noise to show the direction.',
    howToRead: 'When the price (black line) is above the blue line, the short-term trend is up; below it, down.',
  },
  bollinger: {
    title: 'Bollinger Bands (green and red dashed lines)',
    what: 'A "normal range" drawn around the 20-day average. About 95% of the time the price stays inside the bands.',
    howToRead: 'Price near the top band = expensive compared with recent prices; near the bottom band = cheap. Breaking outside the bands means an unusually big move, which often (but not always) partly reverses.',
  },
  rsi: {
    title: 'RSI (Relative Strength Index)',
    what: 'A 0-100 gauge of how strongly the price has been rising or falling over the last 14 periods.',
    howToRead: 'Above 70 = "overbought": it has risen fast and may pause or pull back. Below 30 = "oversold": it has fallen fast and may bounce. 30-70 is normal.',
  },
  macd: {
    title: 'MACD (momentum)',
    what: 'Compares a fast and a slow moving average to show whether the price is speeding up or slowing down.',
    howToRead: 'Green bars (MACD above its signal line) = upward momentum is building. Red bars = momentum is fading or turning down.',
  },
  volatility: {
    title: 'Volatility',
    what: 'How much the price typically swings over a year, as a %.',
    howToRead: 'Under 10% is calm (like a savings-type fund), 10-25% moderate, 25-40% high (like a stock index), over 40% very high. Higher volatility means bigger possible gains and losses.',
  },
  drawdown: {
    title: 'Max drawdown',
    what: 'The biggest fall from a high point to a later low point during the last year.',
    howToRead: 'It shows the worst loss someone who bought at the peak would have seen. -22% means at one point it was 22% below its high.',
  },
  range52w: {
    title: '52-week range',
    what: 'The lowest and highest price over the last year, with a marker showing where today\'s price sits.',
    howToRead: 'Marker at the left = near the year\'s low; at the right = near the year\'s high.',
  },
  payouts: {
    title: 'Payouts (dividends)',
    what: 'Cash a fund pays out per unit, usually once a year in June. On that day the fund\'s price drops by about the same amount, but investors don\'t lose anything: they get the cash, or more units if they reinvest.',
    howToRead: 'Prices, returns and charts here count payouts as reinvested, so a payout does not show as a fall. The latest price is the real price. "Last 12 months" is the payouts of the past year as a share of today\'s price.',
  },
  portfolio: {
    title: 'Portfolio mix',
    what: 'Splitting your money between different investments, for example 60% in a stock fund, 20% in a gold fund and 20% in a cash fund. Stocks and gold often move differently, and the cash part never falls, so the mix usually falls less than any one of its risky parts.',
    howToRead: 'Compare "Return per year" with "Worst fall": a mix that earns a little less but falls much less can be easier to stick with in a crash.',
  },
  rebalance: {
    title: 'Rebalancing',
    what: 'Bringing the mix back to its target split on a schedule, for example every January. After stocks rise, you sell some stocks and buy the parts that fell behind.',
    howToRead: '"Never" lets the split drift with prices. Yearly is common; more often means more trading costs.',
  },
  trendFilter: {
    title: 'Trend filter',
    what: 'Each stock or gold part is moved to the cash fund while its own long-term trend is Down, and bought back when it turns Up.',
    howToRead: 'It aims to avoid long falls, but it trades more and can sell near the bottom and buy back higher. The table shows whether it helped.',
  },
  sharpe: {
    title: 'Sharpe ratio',
    what: 'How much return a mix earned above the cash fund for each unit of ups and downs (volatility).',
    howToRead: 'Higher is better. It lets you compare a calm mix with a bumpy one: a mix with a lower return can still have a higher Sharpe ratio.',
  },
  cashFund: {
    title: 'Cash and income funds',
    what: 'Low-risk Meezan funds. Cash funds (Meezan Cash Fund, Rozana Amdani) invest in very short-term Islamic deposits and government papers; income funds (Islamic Income Fund, Munafa Plan I) in slightly longer Islamic bonds and deposits. Their price creeps up almost every day and rarely falls.',
    howToRead: 'They show what money earns while it waits. Buy/Sell signals don\'t apply to them. They pay their return out as payouts: yearly for most, daily for Rozana Amdani, whose price stays at Rs 50. Returns here count payouts as reinvested.',
  },
  liveRecord: {
    title: 'Live record',
    what: 'The signals the app showed each day, saved at the time, then scored by how prices moved over the following 20 trading days (starting from the next day\'s close).',
    howToRead: 'Compare each row with "Any day": if readings that said Buy were followed by bigger rises than an ordinary day, the signal is adding something. Instruments that move together count separately, so it takes about a year before the numbers mean much.',
  },
  strongerThanCash: {
    title: 'Stronger than cash',
    what: 'Once a month, on the first trading day, compare what the instrument earned over the past 12 months with what the Meezan Cash Fund earned. Stay invested while it is ahead; wait in cash while it is behind.',
    howToRead: 'It is about avoiding long, deep falls, not about timing the best days: it switches about once a year. Tested in advance on Meezan Islamic Fund from 2006, it kept similar returns while cutting the worst fall from about -60% to -25%. It was tested on stocks only, and about four big falls decide the result, so treat it as a guide, not a guarantee.',
  },
  backtest: {
    title: 'Track record (backtest)',
    what: 'A replay of a rule over past prices: invested while the rule says so, in cash otherwise, paying a cost on every trade. Each day uses only the prices known on that day, and trades happen at the next day\'s close.',
    howToRead: 'Compare it with "holding": buying once and doing nothing. If the rule earned less, it would not have been worth acting on in that period. Past results don\'t guarantee future ones.',
  },
  goldenCross: {
    title: 'Golden cross (the trend label)',
    what: 'The rule behind the long-term trend label: stay invested while the 50-day average price is above the 200-day average, and hold cash when it is below.',
    howToRead: 'It only reacts to long trends, so it trades about once or twice a year. It usually gets out after a fall has started and back in after a rise has started.',
  },
  monthlyTrend: {
    title: 'Monthly trend check',
    what: 'A slow trend rule: once a month, on the first trading day, invest if the price is above its 200-day average, otherwise hold cash.',
    howToRead: 'Checking only once a month avoids reacting to daily noise. Between checks, nothing changes, even if the price moves a lot.',
  },
  annualReturn: {
    title: 'Return per year',
    what: 'The steady yearly growth that would turn the starting amount into the final amount.',
    howToRead: '+20% a year means 100 would have grown by a fifth each year on average, reaching about 249 after five years.',
  },
  tradingCost: {
    title: 'Cost per trade',
    what: 'The broker fees, taxes and price gap paid each time you buy or sell, as a % of the amount.',
    howToRead: 'Check your own broker or fund. Mutual funds can charge much more than shares through entry or exit fees. The more often a strategy trades, the more this matters.',
  },
  newsMood: {
    title: 'News mood (sentiment)',
    what: 'An automatic reading of whether recent headlines about this instrument sound positive or negative for its price, from -100 (very negative) to +100 (very positive).',
    howToRead: 'Confidence shows how sure the reading is: it rises with more articles that agree. The reading is based on words like "surges" or "slumps", so it can misjudge complicated headlines; open the articles to check.',
  },
  confidence: {
    title: 'Confidence',
    what: 'How sure the news reading is, from 0% to 100%.',
    howToRead: 'Low (under 40%) = few articles or mixed wording, take it lightly. High (over 70%) = many headlines pointing the same way.',
  },
  background: {
    title: 'Background news',
    what: 'Wider news that can influence this instrument indirectly, like interest rates, the IMF or oil prices.',
    howToRead: 'Worth reading for context, but its tone isn\'t counted in this instrument\'s news mood because it isn\'t directly about it.',
  },
  bigMoves: {
    title: 'Biggest moves and the news around them',
    what: 'The days with the largest price changes recently, with the news published from the day before to the day after each one.',
    howToRead: 'News around a move suggests possible reasons, but it doesn\'t prove cause: markets also move on things that never make the headlines.',
  },
} satisfies Record<string, GlossaryEntry>

export type GlossaryKey = keyof typeof glossary
