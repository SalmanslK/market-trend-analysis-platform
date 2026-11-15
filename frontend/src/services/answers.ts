import { BacktestResult, InstrumentDto, Performance, PortfolioComparison } from './types/marketData'
import { MIN_RELIABLE_YEARS, signedPct } from './plainLanguage'
import { formatDate, STALE_AFTER_DAYS } from './format'

/** A page's short answer: one or two sentences, the numbers behind it, and what it means for the reader. */
export interface PageAnswer {
  answer: string
  facts: { label: string; value: string; note?: string; emphasis?: boolean }[]
  meaning: string
}

const average = (values: number[]) => values.reduce((sum, v) => sum + v, 0) / Math.max(1, values.length)

/**
 * Track Record: would following the technical signal or the long-term trend have beaten simply holding?
 * Averaged over instruments with enough history to judge.
 */
export function trackRecordAnswer(results: BacktestResult[]): PageAnswer | null {
  const rule = (r: BacktestResult, key: 'signal' | 'goldenCross') => r.rules.find((x) => x.key === key)?.performance
  // Only results with both rules, so a missing rule can't be averaged in as a 0% return with no fall
  const reliable = results.filter((r) => r.hasEnoughData && r.years >= MIN_RELIABLE_YEARS && rule(r, 'signal') && rule(r, 'goldenCross'))
  if (reliable.length === 0) return null
  // The average return a year and worst fall of one way of investing, over the reliable results
  const averaged = (pick: (r: BacktestResult) => Performance | undefined) => ({
    annual: average(reliable.map((r) => pick(r)?.annualReturn ?? 0)),
    fall: average(reliable.map((r) => pick(r)?.maxDrawdown ?? 0)),
  })
  const hold = averaged((r) => r.buyAndHold)
  const signal = averaged((r) => rule(r, 'signal'))
  const trend = averaged((r) => rule(r, 'goldenCross'))
  const years = average(reliable.map((r) => r.years))

  const best = [
    { name: 'Simply holding', ...hold },
    { name: 'Following the technical signal', ...signal },
    { name: 'Following the long-term trend', ...trend },
  ].sort((a, b) => b.annual - a.annual)[0]
  const holdingWon = best.name === 'Simply holding'
  // A rule "cushions" falls when its worst fall is clearly smaller than holding's (falls are negative numbers)
  const cushions = [signal, trend].some((x) => x.fall > hold.fall + 2)

  return {
    answer: holdingWon
      ? `Over about ${years.toFixed(0)} years, simply holding earned the most: ${signedPct(hold.annual)} a year on average.${cushions ? ' Following the signals earned less, but some of them had smaller falls.' : ''}`
      : `Over about ${years.toFixed(0)} years, ${best.name.toLowerCase()} earned the most: ${signedPct(best.annual)} a year on average, against ${signedPct(hold.annual)} for simply holding.`,
    facts: [
      { label: 'Simply holding', value: `${signedPct(hold.annual)} a year`, note: `worst fall ${hold.fall.toFixed(0)}%`, emphasis: holdingWon },
      { label: 'Technical signal', value: `${signedPct(signal.annual)} a year`, note: `worst fall ${signal.fall.toFixed(0)}%`, emphasis: best.name.includes('signal') },
      { label: 'Long-term trend', value: `${signedPct(trend.annual)} a year`, note: `worst fall ${trend.fall.toFixed(0)}%`, emphasis: best.name.includes('trend') },
    ],
    meaning: holdingWon
      ? 'The signals are better at warning you about falls than at beating the market. Use them as a warning light, not as a reason to trade often; every trade costs money.'
      : 'Over this period the rule did better than holding, but it trades and was tested on past prices only. Treat it as one input, not a promise.',
  }
}

/** Portfolio: does spreading money between stocks, gold and cash help? */
export function portfolioAnswer(comparison: PortfolioComparison): PageAnswer | null {
  const find = (name: string) => comparison.results.find((r) => r.name === name)
  const stocks = find('All in stocks (MIF)')
  const mix = find('Stocks 60, gold 20, cash 20')
  const cash = find('All in cash (MCF)')
  if (!stocks || !mix) return null
  const lessReturn = stocks.annualReturn - mix.annualReturn
  // Falls are negative numbers, so a higher one is smaller
  const smallerFall = mix.maxDrawdown > stocks.maxDrawdown
  const falls = `${mix.maxDrawdown.toFixed(0)}% against ${stocks.maxDrawdown.toFixed(0)}%`

  return {
    answer: lessReturn > 0
      ? smallerFall
        ? `Spreading money between stocks, gold and cash cut the worst fall from ${stocks.maxDrawdown.toFixed(0)}% to ${mix.maxDrawdown.toFixed(0)}%, for ${lessReturn.toFixed(1)} points a year less return.`
        : `Spreading money between stocks, gold and cash earned ${lessReturn.toFixed(1)} points a year less than all stocks, without a smaller worst fall (${falls}).`
      : `Spreading money between stocks, gold and cash earned ${lessReturn < 0 ? 'more than' : 'as much as'} all stocks (${signedPct(mix.annualReturn)} a year against ${signedPct(stocks.annualReturn)})${smallerFall ? `, with a smaller worst fall (${falls})` : ''}.`,
    facts: [
      { label: 'All in stocks', value: `${signedPct(stocks.annualReturn)} a year`, note: `worst fall ${stocks.maxDrawdown.toFixed(0)}%` },
      { label: 'Stocks 60, gold 20, cash 20', value: `${signedPct(mix.annualReturn)} a year`, note: `worst fall ${mix.maxDrawdown.toFixed(0)}%`, emphasis: true },
      ...(cash ? [{ label: 'All in cash', value: `${signedPct(cash.annualReturn)} a year`, note: 'hardly ever falls' }] : []),
    ],
    meaning: 'If a fall like the all-stocks one would make you sell in a panic, a mix is easier to stick with. If you can sit through big falls for years, more stocks earned more. Try your own mix below.',
  }
}

/** Whether an instrument's newest price is too old: more than STALE_AFTER_DAYS, which allows for weekends and holidays. */
export function isPriceStale(instrument: InstrumentDto, now = Date.now()): boolean {
  return !instrument.latestPriceDate || now - new Date(instrument.latestPriceDate).getTime() > STALE_AFTER_DAYS * 86_400_000
}

function checkedAgo(hours: number | null): string {
  if (hours == null) return 'never'
  if (hours < 1) return 'less than an hour ago'
  if (hours === 1) return '1 hour ago'
  return `${hours} hours ago`
}

/**
 * Settings: is the data up to date? Judged by each instrument's newest price, not by when the app last checked:
 * a check succeeds even when the source has published nothing new.
 */
export function dataStatusAnswer(instruments: InstrumentDto[], now = Date.now()): PageAnswer {
  if (instruments.length === 0) {
    return {
      answer: 'No instruments were found.',
      facts: [],
      meaning: 'The database may be empty or still loading. Press "Check for new data now" below; if nothing appears, restart the app (start-app.cmd).',
    }
  }
  const behind = instruments.filter((i) => isPriceStale(i, now))
  const latest = instruments.map((i) => (i.lastUpdateDate ? new Date(i.lastUpdateDate).getTime() : 0)).reduce((a, b) => Math.max(a, b), 0)
  const hours = latest ? Math.max(0, Math.round((now - latest) / 3_600_000)) : null
  const last = checkedAgo(hours)
  const newest = (i: InstrumentDto) => (i.latestPriceDate ? `${i.symbol} (newest price ${formatDate(i.latestPriceDate)})` : `${i.symbol} (no prices)`)

  return behind.length === 0
    ? {
      answer: `Yes: all ${instruments.length} instruments have prices from the last few days. The last check was ${last}.`,
      facts: [],
      meaning: 'Nothing to do. Prices are checked every 6 hours while the app runs; use the button below if you want the latest right now.',
    }
    : {
      answer: `${behind.length} of ${instruments.length} instruments have no price from the last ${STALE_AFTER_DAYS} days: ${behind.map(newest).join(', ')}. The last check was ${last}.`,
      facts: [],
      meaning: 'Press "Check for new data now" below. If they stay behind, their source may have stopped publishing or the app may need restarting (start-app.cmd).',
    }
}
