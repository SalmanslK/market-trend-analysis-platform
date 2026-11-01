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
  signal: {
    title: 'Technical signal',
    what: 'A summary of five common chart indicators. Each one votes +1 (points up), -1 (points down) or 0 (no view). The total gives Strong Buy, Buy, Hold, Sell or Strong Sell.',
    howToRead: 'Treat it as a quick read of the price trend, not a recommendation. It looks only at past prices: it knows nothing about company results, news, dividends or your own goals.',
  },
  score: {
    title: 'Signal score',
    what: 'The total of the indicator votes, e.g. "-1/5" means five indicators were checked and one more pointed down than up.',
    howToRead: '+3 or more is Strong Buy, +1 to +2 Buy, 0 Hold, -1 to -2 Sell, -3 or less Strong Sell. The closer to zero, the more mixed the picture.',
  },
  trend: {
    title: 'Trend (moving averages)',
    what: 'A moving average is the average price over the last N days. The 200-day average shows the long-term direction, the 50-day the medium term.',
    howToRead: 'Price above its average = uptrend (prices have been rising). Price below = downtrend.',
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
