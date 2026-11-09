import { BacktestResult, BacktestVerdict, InstrumentAnalyticsDto, InstrumentType, NewsSentimentSummary, SignalAction, PortfolioComparison } from './types/marketData'
import { formatDate, formatNumber } from './format'

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

/** The long-term trend in a sentence or two. */
export function describeTrend(a: InstrumentAnalyticsDto): string {
  const { direction, since } = a.longTermTrend
  if (direction === 'Unknown') return 'There are too few prices so far to tell the long-term trend (it needs at least 200).'

  const sinceText = since ? ` This has been the case since ${formatDate(since)}.` : ''
  const meaning = direction === 'Up'
    ? 'Prices over the last 50 days have averaged higher than over the last 200 days.'
    : 'Prices over the last 50 days have averaged lower than over the last 200 days.'
  return `${meaning}${sinceText} This describes the past and reacts slowly, so it can disagree with what the price has done lately; it isn't a prediction.`
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
export function describeInstrument(a: InstrumentAnalyticsDto, mood?: NewsSentimentSummary, news = true): { headline: string; points: string[] } {
  const points: string[] = []

  // Recent performance, from short to long term
  const recent: string[] = []
  if (a.change1M != null) recent.push(`${upDown(a.change1M)} ${pct(a.change1M)} over the past month`)
  if (a.changeYtd != null) recent.push(`${upDown(a.changeYtd)} ${pct(a.changeYtd)} since January`)
  if (a.change1Y != null) recent.push(`${upDown(a.change1Y)} ${pct(a.change1Y)} over 12 months`)
  if (recent.length) points.push(`It is ${recent.join(', ')}.`)

  const { direction, since } = a.longTermTrend
  const momentum = a.signal.reasons.find((r) => r.indicator === 'Momentum (MACD)')
  if (direction !== 'Unknown') {
    const sinceText = since ? ` since ${formatDate(since)}` : ''
    const turn = momentum && (momentum.impact > 0) !== (direction === 'Up')
      ? momentum.impact > 0 ? ', although recent momentum has started to turn up' : ', although recent momentum has started to fade'
      : ''
    points.push(`The long-term trend has been ${direction.toLowerCase()}${sinceText}${turn}.`)
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
  // The short-term signal and long-term trend measure different things, so spell out when they differ
  if (direction !== 'Unknown' && a.signal.score !== 0 && (a.signal.score > 0) !== (direction === 'Up')) {
    points.push(
      a.signal.score > 0
        ? 'The signal leans towards Buy while the long-term trend is still down: prices have recently risen within a longer weak period.'
        : 'The signal leans towards Sell while the long-term trend is still up: prices have recently weakened within a longer strong period.',
    )
  }

  if (mood && mood.articles > 0 && mood.label !== 'Neutral' && a.signal.score !== 0) {
    const signalUp = a.signal.score > 0
    const newsUp = mood.label === 'Positive'
    // When they disagree, say why: they look at different periods, so after a sharp turn the news moves first
    const dailyAgrees = a.signal.steady && a.dailySignal && a.dailySignal.score !== 0 && (a.dailySignal.score > 0) === newsUp
    points.push(
      signalUp === newsUp
        ? `The signal and the news point the same way (${newsUp ? 'up' : 'down'}), which makes the picture clearer.`
        : `The technical signal points ${signalUp ? 'up' : 'down'} but the news sounds ${newsUp ? 'positive' : 'negative'}. They look at different periods: ` +
          (a.signal.steady
            ? 'the news covers the last 7 days, the steady technical signal the last 20 trading days, so after a sharp turn they disagree until the move lasts.'
            : 'the news covers the last 7 days, the technical signal where today\'s price stands against its averages.') +
          (dailyAgrees ? ' Today\'s votes on their own already agree with the news.' : '') +
          ' Until they agree, the combined signal says Hold.',
    )
  }

  const note = directionNote(a.symbol)
  if (note) points.push(note)

  const labels: Record<SignalAction, string> = { StrongBuy: 'Strong Buy', Buy: 'Buy', Hold: 'Hold', Sell: 'Sell', StrongSell: 'Strong Sell' }
  if (news) {
    const newsText = a.newsSignal.articles >= 3 ? labels[a.newsSignal.action] : `Hold (only ${a.newsSignal.articles} recent ${a.newsSignal.articles === 1 ? 'story' : 'stories'})`
    const combinedText = a.combinedSignal.action === 'Hold' ? 'Hold (stay as you are), because technical and news don\'t clearly agree' : labels[a.combinedSignal.action]
    points.unshift(`Combined signal: ${combinedText}. Technical says ${labels[a.signal.action]}, news says ${newsText}.`)
  } else {
    const technical = a.signal.action === 'Hold' ? 'Hold (stay as you are)' : labels[a.signal.action]
    points.unshift(`Technical signal: ${technical}${a.signal.steady ? ', from the last 20 trading days' : ', from today\'s prices'}.`)
  }

  return {
    headline: `${signalMeaning[a.signal.action].headline}. ${a.name} is at ${formatNumber(a.lastPrice)} (${priceMeaning(a)}).`,
    points,
  }
}

/**
 * A few sentences describing the whole market from all instruments' analytics.
 */
export function describeMarket(everything: InstrumentAnalyticsDto[], moods: NewsSentimentSummary[], news = true): string[] {
  // A cash fund is a reference for what money earns, not part of the market's mood
  const all = everything.filter((a) => !a.isCash)
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

  const leaning = (action: string) => action === 'StrongBuy' || action === 'Buy' ? 'buy' : action === 'StrongSell' || action === 'Sell' ? 'sell' : 'hold'
  const buys = all.filter((a) => leaning(a.combinedSignal.action) === 'buy').length
  const sells = all.filter((a) => leaning(a.combinedSignal.action) === 'sell').length
  sentences.push(news
    ? `Combined signal (Buy or Sell only when technical and news agree): ${buys} Buy, ${sells} Sell, ${all.length - buys - sells} Hold (stay as you are).`
    : `Technical signal: ${buys} Buy, ${sells} Sell, ${all.length - buys - sells} Hold (stay as you are).`)

  const trendsUp = all.filter((a) => a.longTermTrend.direction === 'Up').length
  const trendsDown = all.filter((a) => a.longTermTrend.direction === 'Down').length
  const recovering = all.filter((a) => a.longTermTrend.phase === 'Recovering').map((a) => a.symbol)
  const weakening = all.filter((a) => a.longTermTrend.phase === 'Weakening').map((a) => a.symbol)
  const phases = [
    recovering.length ? `${recovering.join(', ')} already recovering` : '',
    weakening.length ? `${weakening.join(', ')} already weakening` : '',
  ].filter(Boolean).join('; ')
  sentences.push(`Long-term trend: up for ${trendsUp}, down for ${trendsDown}${phases ? `; ${phases}` : ''}.`)

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

  // Instruments linked to the same stories have one shared reading, not separate ones
  const groups = new Map<string, string[]>()
  for (const a of all.filter((a) => a.newsSignal.sharedWith.length)) {
    const key = [a.symbol, ...a.newsSignal.sharedWith].sort().join(', ')
    groups.set(key, key.split(', '))
  }
  // One line for all groups: it's a caveat, not news
  if (news && groups.size > 0)
    sentences.push(`Some instruments read the same news, so their news signals move together: ${[...groups.values()].map((g) => g.join(' & ')).join('; ')}.`)

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

/** How acting on a rule would have compared with holding, for one instrument. */
export function describeRuleRecord(r: BacktestResult, key: 'signal' | 'goldenCross' | 'monthlyTrend'): string {
  const rule = r.rules.find((x) => x.key === key)
  if (!rule) return ''

  const { annualReturn, maxDrawdown, trades } = rule.performance
  const holding = r.buyAndHold
  const result = compareWithHolding(annualReturn, holding.annualReturn)
  const outcome = result === 'tied' ? 'about the same as' : result === 'ahead' ? 'more than' : 'less than'
  const short = r.years < MIN_RELIABLE_YEARS ? ' This is too little history to judge.' : ''

  return `Over ${r.years.toFixed(1)} years, ${signedPct(annualReturn)} a year after costs with ${trades} trades: ${outcome} simply holding (${signedPct(holding.annualReturn)}). Worst fall ${maxDrawdown.toFixed(0)}% against ${holding.maxDrawdown.toFixed(0)}%.${short}`
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
    `At ${costPercent}% per trade, following the Buy/Sell signal beat simply holding on ${helped} of ${tested.length} instruments, lowered risk at a lower return on ${lowerRisk}, and did worse on ${worse}.`,
  ]
  sentences.push(
    helped * 2 > tested.length
      ? 'At this cost, the signal added value more often than not.'
      : 'At this cost, acting on the signal alone has not beaten simply holding on most instruments.',
  )
  const reliable = tested.filter((r) => r.years >= MIN_RELIABLE_YEARS)
  if (reliable.length > 0) {
    const perYear = reliable.reduce((sum, r) => sum + r.strategy.trades / r.years, 0) / reliable.length
    sentences.push(`It trades about ${Math.round(perYear)} times a year, so costs add up: change the cost at the top of the page to see how much they matter.`)
  }
  if (short.length > 0) sentences.push(`${short.length > 1 ? `${short.slice(0, -1).join(', ')} and ${short[short.length - 1]}` : short[0]} ${short.length > 1 ? 'have' : 'has'} too little history to judge.`)
  return sentences
}

// Differences smaller than this, in percentage points a year, are too small to call a win or a loss
const MEANINGFUL_ANNUAL_DIFFERENCE = 1

export const ruleNames: Record<'signal' | 'goldenCross' | 'monthlyTrend', string> = {
  signal: 'Buy/Sell signal',
  goldenCross: 'Long-term trend',
  monthlyTrend: 'Monthly trend check',
}

/** Compares a rule with holding, treating very small differences as a tie. */
export function compareWithHolding(ruleAnnual: number, holdingAnnual: number): 'ahead' | 'tied' | 'behind' {
  const difference = ruleAnnual - holdingAnnual
  if (Math.abs(difference) < MEANINGFUL_ANNUAL_DIFFERENCE) return 'tied'
  return difference > 0 ? 'ahead' : 'behind'
}

/** The overall finding for the trend label, counted only on instruments with enough history. */
export function describeTrendRules(results: BacktestResult[], costPercent: number): string[] {
  const reliable = results.filter((r) => r.hasEnoughData && r.years >= MIN_RELIABLE_YEARS && r.rules.length > 0)
  if (reliable.length === 0) return []

  const annual = (r: BacktestResult, key: 'signal' | 'goldenCross') =>
    r.rules.find((x) => x.key === key)!.performance.annualReturn
  const tradesPerYear = (key: 'signal' | 'goldenCross') =>
    Math.round(reliable.reduce((sum, r) => sum + r.rules.find((x) => x.key === key)!.performance.trades / r.years, 0) / reliable.length)
  const count = (result: 'ahead' | 'tied' | 'behind') =>
    reliable.filter((r) => compareWithHolding(annual(r, 'goldenCross'), r.buyAndHold.annualReturn) === result).length
  const beatOld = reliable.filter((r) => annual(r, 'goldenCross') > annual(r, 'signal')).length

  return [
    `At ${costPercent}% per trade, on the ${reliable.length} instruments with at least ${MIN_RELIABLE_YEARS} years of history, following the long-term trend was clearly ahead of simply holding on ${count('ahead')}, about the same on ${count('tied')} and behind on ${count('behind')}.`,
    `It beat the Buy/Sell signal on ${beatOld} of ${reliable.length}, mainly by trading far less: about ${tradesPerYear('goldenCross')} times a year instead of ${tradesPerYear('signal')}.`,
    'So simply holding has been hard to beat on this history.',
    'The long-term trend rule makes few trades and was picked after seeing this test, so treat its figures as weak evidence.',
  ]
}

/** The overall finding for the technical, news and combined signals, counted on instruments with enough history. */
export function describeNewsTests(results: BacktestResult[], costPercent: number): string[] {
  const reliable = results.filter((r) => r.newsTest.hasEnoughNews && r.newsTest.years >= MIN_RELIABLE_YEARS)
  if (reliable.length === 0) return ['There is not enough news history yet to test the news and combined signals.']

  const annual = (r: BacktestResult, key: 'technical' | 'news' | 'combined') =>
    r.newsTest.rules.find((x) => x.key === key)!.performance.annualReturn
  const count = (key: 'technical' | 'news' | 'combined', result: 'ahead' | 'tied' | 'behind') =>
    reliable.filter((r) => compareWithHolding(annual(r, key), r.newsTest.holding.annualReturn) === result).length
  const line = (name: string, key: 'technical' | 'news' | 'combined') =>
    `${name}: clearly ahead of holding on ${count(key, 'ahead')}, about the same on ${count(key, 'tied')}, behind on ${count(key, 'behind')}.`
  const combinedBeatTechnical = reliable.filter((r) => annual(r, 'combined') > annual(r, 'technical')).length
  const combinedBeatNews = reliable.filter((r) => annual(r, 'combined') > annual(r, 'news')).length

  return [
    `At ${costPercent}% per trade, on the ${reliable.length} instruments with at least ${MIN_RELIABLE_YEARS} years of news:`,
    line('Technical', 'technical'),
    line('News', 'news'),
    line('Combined', 'combined'),
    `Combined beat technical alone on ${combinedBeatTechnical} of ${reliable.length}, and news alone on ${combinedBeatNews} of ${reliable.length}.`,
  ]
}

/** How the news or combined signal would have compared with holding, for one instrument. */
export function describeNewsRuleRecord(r: BacktestResult, key: 'news' | 'combined'): string {
  const rule = r.newsTest.rules.find((x) => x.key === key)
  if (!rule) return ''

  const { annualReturn, maxDrawdown, trades } = rule.performance
  const holding = r.newsTest.holding
  const result = compareWithHolding(annualReturn, holding.annualReturn)
  const outcome = result === 'tied' ? 'about the same as' : result === 'ahead' ? 'more than' : 'less than'
  const short = r.newsTest.years < MIN_RELIABLE_YEARS ? ' This is too little history to judge.' : ''

  return `Over ${r.newsTest.years.toFixed(1)} years, ${signedPct(annualReturn)} a year after costs with ${trades} trades: ${outcome} simply holding (${signedPct(holding.annualReturn)}). Worst fall ${maxDrawdown.toFixed(0)}% against ${holding.maxDrawdown.toFixed(0)}%.${short}`
}

/**
 * Plain sentences comparing portfolio mixes: which earned most, which fell least, and the trade-off between them.
 */
export function describePortfolios(comparison: PortfolioComparison): string[] {
  const results = comparison.results
  if (results.length === 0) return []
  const sentences: string[] = []
  const years = results[0].years

  const byReturn = [...results].sort((a, b) => b.annualReturn - a.annualReturn)
  const risky = results.filter((r) => r.maxDrawdown < -1)
  const byFall = [...risky].sort((a, b) => b.maxDrawdown - a.maxDrawdown)
  const best = byReturn[0]
  sentences.push(`Over these ${years.toFixed(1)} years, "${best.name}" earned the most: ${best.annualReturn.toFixed(1)}% a year, with a worst fall of ${best.maxDrawdown.toFixed(0)}%.`)

  // Mixes that kept most of the best return for much less fall
  const tradeOffs = risky
    .filter((r) => r !== best && r.annualReturn >= best.annualReturn - 3 && r.maxDrawdown > best.maxDrawdown * 0.75)
    .sort((a, b) => b.annualReturn - a.annualReturn)
  if (tradeOffs.length > 0) {
    const t = tradeOffs[0]
    sentences.push(`"${t.name}" earned ${(best.annualReturn - t.annualReturn).toFixed(1)} points a year less, but its worst fall was ${t.maxDrawdown.toFixed(0)}% instead of ${best.maxDrawdown.toFixed(0)}%.`)
  } else if (byFall.length > 0) {
    const calm = byFall[0]
    sentences.push(`The calmest mix with risky parts, "${calm.name}", fell at most ${calm.maxDrawdown.toFixed(0)}% and earned ${calm.annualReturn.toFixed(1)}% a year.`)
  }

  if (comparison.cashAnnualReturn != null)
    sentences.push(`The cash fund earned ${comparison.cashAnnualReturn.toFixed(1)}% a year over the same dates without falling, so money waiting in cash wasn't idle.`)

  const filtered = results.filter((r) => r.trendFilter)
  for (const f of filtered) {
    const plain = results.find((r) => !r.trendFilter && r.rebalance === f.rebalance && JSON.stringify(r.weights) === JSON.stringify(f.weights))
    if (!plain) continue
    const helped = f.annualReturn > plain.annualReturn && f.maxDrawdown >= plain.maxDrawdown
    const hurt = f.annualReturn < plain.annualReturn && f.maxDrawdown <= plain.maxDrawdown
    sentences.push(`The trend filter on "${plain.name}" ${helped ? 'helped' : hurt ? 'hurt' : 'was mixed'}: ${f.annualReturn.toFixed(1)}% a year and a worst fall of ${f.maxDrawdown.toFixed(0)}%, against ${plain.annualReturn.toFixed(1)}% and ${plain.maxDrawdown.toFixed(0)}% without it.`)
  }

  if (years < MIN_RELIABLE_YEARS)
    sentences.push(`Only ${years.toFixed(1)} years can be compared, because one of the chosen instruments has a short history. One good or bad stretch can decide a result this short.`)

  return sentences
}
