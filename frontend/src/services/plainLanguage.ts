import { BacktestResult, BacktestVerdict, InstrumentAnalyticsDto, InstrumentType, NewsSentimentSummary, SignalAction } from './types/marketData'
import { formatNumber } from './format'

/**
 * Turns numbers into short plain-English sentences for readers without a finance background.
 * Wording describes what the indicators show; it never tells the reader what to do with their money.
 */

const pct = (value: number, decimals = 1) => `${Math.abs(value).toFixed(decimals)}%`
const upDown = (value: number) => (value >= 0 ? 'up' : 'down')

/** What the price of each kind of instrument measures, so movements read naturally. */
export function priceMeaning(a: Pick<InstrumentAnalyticsDto, 'symbol' | 'type'>): string {
  if (a.symbol === 'USDPKR') return 'rupees per US dollar'
  if (a.symbol === 'GOLDINT') return 'US dollars per ounce of gold'
  if (a.symbol === 'GOLDPK') return 'rupees per tola of gold'
  if (a.type === InstrumentType.MutualFund) return 'value of one fund unit (NAV), in rupees'
  return 'index points'
}

/** USD/PKR rising means the rupee is weakening; spell that out wherever the direction matters. */
export function directionNote(symbol: string): string | undefined {
  return symbol === 'USDPKR'
    ? 'For USD/PKR, "up" means the dollar costs more rupees (the rupee is weakening) and "down" means the rupee is getting stronger.'
    : undefined
}

export function volatilityLevel(volatility?: number): { label: string; text: string } | undefined {
  if (volatility == null) return undefined
  if (volatility < 10) return { label: 'Calm', text: 'small, steady price changes' }
  if (volatility < 25) return { label: 'Moderate', text: 'noticeable ups and downs' }
  if (volatility < 40) return { label: 'High', text: 'large price swings' }
  return { label: 'Very high', text: 'very large swings' }
}

export function describeRsi(rsi?: number): string | undefined {
  if (rsi == null) return undefined
  if (rsi > 70) return 'Overbought: it has risen fast and may pause or pull back'
  if (rsi < 30) return 'Oversold: it has fallen fast and may bounce'
  if (rsi >= 55) return 'Normal, leaning towards buying pressure'
  if (rsi <= 45) return 'Normal, leaning towards selling pressure'
  return 'Normal: neither overbought nor oversold'
}

export function describeRange(position?: number): string | undefined {
  if (position == null) return undefined
  if (position >= 90) return 'At or near its highest price of the year'
  if (position >= 65) return 'In the upper part of this year\'s range'
  if (position > 35) return 'Around the middle of this year\'s range'
  if (position > 10) return 'In the lower part of this year\'s range'
  return 'At or near its lowest price of the year'
}

export function describeDrawdown(drawdown?: number): string | undefined {
  if (drawdown == null) return undefined
  if (drawdown > -5) return 'Has barely fallen from its highs this year'
  return `At its worst this year it was ${pct(drawdown)} below its peak`
}

export const signalMeaning: Record<SignalAction, { headline: string; detail: string }> = {
  StrongBuy: {
    headline: 'Most indicators point up',
    detail: 'The price trend and momentum are both positive. Strong upward trends often continue for a while, but can also be late in the move.',
  },
  Buy: {
    headline: 'Indicators lean positive',
    detail: 'More indicators point up than down, but not all of them agree.',
  },
  Hold: {
    headline: 'Indicators are mixed',
    detail: 'Signals cancel each other out: there is no clear direction right now.',
  },
  Sell: {
    headline: 'Indicators lean negative',
    detail: 'More indicators point down than up, but not all of them agree.',
  },
  StrongSell: {
    headline: 'Most indicators point down',
    detail: 'The price trend and momentum are both negative. Falling trends often continue for a while, but can also be close to a turning point.',
  },
}

export function describeMood(mood: NewsSentimentSummary | undefined, symbol: string): string | undefined {
  if (!mood) return undefined
  if (mood.articles === 0) {
    return mood.backgroundArticles > 0
      ? `No news directly about it in the last ${mood.days} days, only ${mood.backgroundArticles} wider economic stories.`
      : `No related news in the last ${mood.days} days.`
  }

  const sure = mood.confidence >= 70 ? 'fairly clearly' : mood.confidence >= 40 ? 'somewhat' : 'only slightly'
  const tone = mood.label === 'Positive'
    ? `sounds ${sure} positive`
    : mood.label === 'Negative'
      ? `sounds ${sure} negative`
      : 'is mixed or neutral'
  const usdNote = symbol === 'USDPKR' && mood.label !== 'Neutral'
    ? mood.label === 'Negative' ? ' (pointing to a stronger rupee)' : ' (pointing to a weaker rupee)'
    : ''

  return `News over the last ${mood.days} days ${tone}${usdNote}: ${mood.positive} positive, ${mood.negative} negative and ${mood.neutral} neutral headlines.`
}

/**
 * A short plain-language summary of one instrument: a headline and a few key points.
 */
export function describeInstrument(a: InstrumentAnalyticsDto, mood?: NewsSentimentSummary): { headline: string; points: string[] } {
  const points: string[] = []

  // Recent performance, from short to long term
  const recent: string[] = []
  if (a.change1M != null) recent.push(`${upDown(a.change1M)} ${pct(a.change1M)} over the past month`)
  if (a.changeYtd != null) recent.push(`${upDown(a.changeYtd)} ${pct(a.changeYtd)} since January`)
  if (a.change1Y != null) recent.push(`${upDown(a.change1Y)} ${pct(a.change1Y)} over 12 months`)
  if (recent.length) points.push(`It is ${recent.join(', ')}.`)

  const longTrend = a.signal.reasons.find((r) => r.indicator === 'Long-term trend')
  const momentum = a.signal.reasons.find((r) => r.indicator === 'Momentum (MACD)')
  if (longTrend) {
    const trend = longTrend.impact > 0 ? 'in a long-term uptrend (above its 200-day average)' : 'in a long-term downtrend (below its 200-day average)'
    const turn = momentum && momentum.impact !== longTrend.impact
      ? momentum.impact > 0 ? ', although recent momentum has started to turn up' : ', although recent momentum has started to fade'
      : ''
    points.push(`The price is ${trend}${turn}.`)
  }

  const range = describeRange(a.rangePosition52W)
  const rsi = describeRsi(a.rsi14)
  if (range) points.push(`${range}.${rsi ? ` RSI reading: ${rsi.charAt(0).toLowerCase()}${rsi.slice(1)}.` : ''}`)

  const vol = volatilityLevel(a.volatility1Y)
  if (vol && a.maxDrawdown1Y != null) {
    points.push(`Risk is ${vol.label.toLowerCase()} (${vol.text}). ${describeDrawdown(a.maxDrawdown1Y)}.`)
  }

  const moodText = describeMood(mood, a.symbol)
  if (moodText) points.push(moodText)

  // Price trend and news agreeing makes the picture clearer; disagreeing is a reason for extra care
  if (mood && mood.articles > 0 && mood.label !== 'Neutral' && a.signal.score !== 0) {
    const signalUp = a.signal.score > 0
    const newsUp = mood.label === 'Positive'
    points.push(
      signalUp === newsUp
        ? `The price trend and the news point the same way (${newsUp ? 'up' : 'down'}), which makes the picture clearer.`
        : `The price trend points ${signalUp ? 'up' : 'down'} but the news sounds ${newsUp ? 'positive' : 'negative'}. When they disagree, the picture is less certain: worth reading the news before deciding.`,
    )
  }

  const note = directionNote(a.symbol)
  if (note) points.push(note)

  const meaning = signalMeaning[a.signal.action]
  return {
    headline: `${meaning.headline}. ${a.name} is at ${formatNumber(a.lastPrice)} (${priceMeaning(a)}).`,
    points,
  }
}

/**
 * A few sentences describing the whole market from all instruments' analytics.
 */
export function describeMarket(all: InstrumentAnalyticsDto[], moods: NewsSentimentSummary[]): string[] {
  if (all.length === 0) return []
  const sentences: string[] = []

  const ups = all.filter((a) => (a.change1M ?? 0) > 0).length
  const downs = all.filter((a) => (a.change1M ?? 0) < 0).length
  sentences.push(
    downs > ups
      ? `It has been a weak month: ${downs} of ${all.length} instruments are lower than a month ago.`
      : ups > downs
        ? `It has been a good month: ${ups} of ${all.length} instruments are higher than a month ago.`
        : 'A mixed month: about as many instruments are up as down.',
  )

  const byMonth = all.filter((a) => a.change1M != null).sort((x, y) => (y.change1M ?? 0) - (x.change1M ?? 0))
  if (byMonth.length >= 2) {
    const best = byMonth[0]
    const worst = byMonth[byMonth.length - 1]
    sentences.push(
      (best.change1M! >= 0
        ? `Best this month: ${best.name} (+${best.change1M!.toFixed(1)}%). `
        : `${best.name} fell the least (${best.change1M!.toFixed(1)}%). `) +
      `Weakest: ${worst.name} (${worst.change1M!.toFixed(1)}%).`,
    )
  }

  const negativeSignals = all.filter((a) => a.signal.score < 0).length
  const positiveSignals = all.filter((a) => a.signal.score > 0).length
  sentences.push(
    negativeSignals > positiveSignals
      ? `Technical signals lean negative for ${negativeSignals} of ${all.length} instruments, so prices have mostly been trending down.`
      : positiveSignals > negativeSignals
        ? `Technical signals lean positive for ${positiveSignals} of ${all.length} instruments, so prices have mostly been trending up.`
        : 'Technical signals are evenly split.',
  )

  const scored = moods.filter((m) => m.articles > 0)
  const positiveMood = scored.filter((m) => m.label === 'Positive').map((m) => m.symbol)
  const negativeMood = scored.filter((m) => m.label === 'Negative').map((m) => m.symbol)
  if (positiveMood.length || negativeMood.length) {
    // USD/PKR reads the other way round: negative news for it means a stronger rupee
    const name = (symbol: string, label: string) =>
      symbol === 'USDPKR' ? `USDPKR (${label === 'Negative' ? 'a stronger rupee' : 'a weaker rupee'})` : symbol
    const parts: string[] = []
    if (positiveMood.length) parts.push(`positive for ${positiveMood.map((s) => name(s, 'Positive')).join(', ')}`)
    if (negativeMood.length) parts.push(`negative for ${negativeMood.map((s) => name(s, 'Negative')).join(', ')}`)
    sentences.push(`This week's news sounds ${parts.join(' and ')}.`)
  }

  return sentences
}

// Under this much testable history, one good or bad stretch can decide the result
export const MIN_RELIABLE_YEARS = 3

export const verdictStyles: Record<BacktestVerdict, { label: string; background: string; color: string }> = {
  Helped: { label: 'Beat holding', background: '#c8e6c9', color: '#1b5e20' },
  LowerRisk: { label: 'Lower risk, lower return', background: '#fff3cd', color: '#7a5b00' },
  DidNotHelp: { label: 'Did worse than holding', background: '#ffcdd2', color: '#b71c1c' },
}

const signedPct = (value: number) => `${value >= 0 ? '+' : ''}${value.toFixed(1)}%`

/** One instrument's track record in a sentence. */
export function describeBacktest(r: BacktestResult): string {
  const comparison = `Over ${r.years.toFixed(1)} years, following the signal earned ${signedPct(r.strategy.annualReturn)} a year after costs, against ${signedPct(r.buyAndHold.annualReturn)} a year for simply holding.`
  const fall = r.strategy.maxDrawdown > r.buyAndHold.maxDrawdown
    ? ` Its worst fall was smaller (${r.strategy.maxDrawdown.toFixed(0)}% against ${r.buyAndHold.maxDrawdown.toFixed(0)}%).`
    : ` Its worst fall was no smaller (${r.strategy.maxDrawdown.toFixed(0)}% against ${r.buyAndHold.maxDrawdown.toFixed(0)}%).`
  const short = r.years < MIN_RELIABLE_YEARS ? ' This is too little history to judge.' : ''
  return comparison + fall + short
}

/** The overall finding across all instruments. */
export function describeBacktests(results: BacktestResult[], costPercent: number): string[] {
  const tested = results.filter((r) => r.hasEnoughData)
  if (tested.length === 0) return []

  const helped = tested.filter((r) => r.verdict === 'Helped').length
  const lowerRisk = tested.filter((r) => r.verdict === 'LowerRisk').length
  const worse = tested.length - helped - lowerRisk
  const short = tested.filter((r) => r.years < MIN_RELIABLE_YEARS).map((r) => r.symbol)

  const sentences = [
    `At ${costPercent}% per trade, following the signal beat simply holding on ${helped} of ${tested.length} instruments, lowered risk at a lower return on ${lowerRisk}, and did worse on ${worse}.`,
  ]
  sentences.push(
    helped * 2 > tested.length
      ? 'On this history, the signal has added value more often than not.'
      : 'On this history, the signal alone has not been a reliable reason to buy or sell. Treat it as a description of the current trend, not as a prediction.',
  )
  const reliable = tested.filter((r) => r.years >= MIN_RELIABLE_YEARS)
  if (reliable.length > 0) {
    const perYear = reliable.reduce((sum, r) => sum + r.strategy.trades / r.years, 0) / reliable.length
    sentences.push(`It trades about ${Math.round(perYear)} times a year, so costs add up: change the cost below to see how much they matter.`)
  }
  if (short.length > 0) sentences.push(`${short.length > 1 ? `${short.slice(0, -1).join(', ')} and ${short[short.length - 1]}` : short[0]} ${short.length > 1 ? 'have' : 'has'} too little history to judge.`)
  return sentences
}

// Differences smaller than this, in percentage points a year, are too small to call a win or a loss
const MEANINGFUL_ANNUAL_DIFFERENCE = 1

export const ruleNames: Record<'signal' | 'goldenCross' | 'monthlyTrend', string> = {
  signal: 'Current signal',
  goldenCross: 'Golden cross',
  monthlyTrend: 'Monthly trend check',
}

/** Compares a rule with holding, treating very small differences as a tie. */
export function compareWithHolding(ruleAnnual: number, holdingAnnual: number): 'ahead' | 'tied' | 'behind' {
  const difference = ruleAnnual - holdingAnnual
  if (Math.abs(difference) < MEANINGFUL_ANNUAL_DIFFERENCE) return 'tied'
  return difference > 0 ? 'ahead' : 'behind'
}

/** The overall finding for the slower rules, counted only on instruments with enough history. */
export function describeSlowerRules(results: BacktestResult[]): string[] {
  const reliable = results.filter((r) => r.hasEnoughData && r.years >= MIN_RELIABLE_YEARS && r.rules.length > 0)
  if (reliable.length === 0) return []

  const annual = (r: BacktestResult, key: 'signal' | 'goldenCross' | 'monthlyTrend') =>
    r.rules.find((x) => x.key === key)!.performance.annualReturn
  const trades = (r: BacktestResult, key: 'signal' | 'goldenCross' | 'monthlyTrend') =>
    r.rules.find((x) => x.key === key)!.performance.trades / r.years

  const count = (key: 'goldenCross' | 'monthlyTrend', result: 'ahead' | 'tied' | 'behind') =>
    reliable.filter((r) => compareWithHolding(annual(r, key), r.buyAndHold.annualReturn) === result).length
  const beatSignal = (key: 'goldenCross' | 'monthlyTrend') =>
    reliable.filter((r) => annual(r, key) > annual(r, 'signal')).length
  const average = (key: 'signal' | 'goldenCross' | 'monthlyTrend') =>
    Math.round(reliable.reduce((sum, r) => sum + trades(r, key), 0) / reliable.length)

  const summary = (key: 'goldenCross' | 'monthlyTrend') =>
    `${ruleNames[key]} beat the current signal on ${beatSignal(key)} of ${reliable.length} instruments. Against holding it was clearly ahead on ${count(key, 'ahead')}, about the same on ${count(key, 'tied')} and behind on ${count(key, 'behind')}.`

  return [
    `Counting the ${reliable.length} instruments with at least ${MIN_RELIABLE_YEARS} years of history:`,
    summary('goldenCross'),
    summary('monthlyTrend'),
    `The reason is mainly fewer trades: about ${Math.max(average('goldenCross'), average('monthlyTrend'))} a year instead of about ${average('signal')}, so far less is lost to costs and short-lived moves.`,
    'So slower rules avoided most of the current signal\'s damage, but they have not reliably beaten simply holding.',
  ]
}
