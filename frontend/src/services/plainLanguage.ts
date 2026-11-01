import { InstrumentAnalyticsDto, InstrumentType, NewsSentimentSummary, SignalAction } from './types/marketData'
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
