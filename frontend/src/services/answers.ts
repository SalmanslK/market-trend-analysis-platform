import { BacktestResult, InstrumentDto, PortfolioComparison } from './types/marketData'
import { MIN_RELIABLE_YEARS } from './plainLanguage'

/** A page's short answer: one or two sentences, the numbers behind it, and what it means for the reader. */
export interface PageAnswer {
  answer: string
  facts: { label: string; value: string; note?: string; emphasis?: boolean }[]
  meaning: string
}

const pct = (value: number, digits = 1) => `${value >= 0 ? '+' : ''}${value.toFixed(digits)}%`
const average = (values: number[]) => values.reduce((sum, v) => sum + v, 0) / Math.max(1, values.length)

/**
 * Track Record: would following the technical signal or the long-term trend have beaten simply holding?
 * Averaged over instruments with enough history to judge.
 */
export function trackRecordAnswer(results: BacktestResult[]): PageAnswer | null {
  const reliable = results.filter((r) => r.hasEnoughData && r.years >= MIN_RELIABLE_YEARS)
  if (reliable.length === 0) return null
  const rule = (r: BacktestResult, key: 'signal' | 'goldenCross') => r.rules.find((x) => x.key === key)?.performance
  const hold = { annual: average(reliable.map((r) => r.buyAndHold.annualReturn)), fall: average(reliable.map((r) => r.buyAndHold.maxDrawdown)) }
  const signal = { annual: average(reliable.map((r) => rule(r, 'signal')?.annualReturn ?? 0)), fall: average(reliable.map((r) => rule(r, 'signal')?.maxDrawdown ?? 0)) }
  const trend = { annual: average(reliable.map((r) => rule(r, 'goldenCross')?.annualReturn ?? 0)), fall: average(reliable.map((r) => rule(r, 'goldenCross')?.maxDrawdown ?? 0)) }
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
      ? `Over about ${years.toFixed(0)} years, simply holding earned the most: ${pct(hold.annual)} a year on average.${cushions ? ' Following the signals earned less, but some of them had smaller falls.' : ''}`
      : `Over about ${years.toFixed(0)} years, ${best.name.toLowerCase()} earned the most: ${pct(best.annual)} a year on average, against ${pct(hold.annual)} for simply holding.`,
    facts: [
      { label: 'Simply holding', value: `${pct(hold.annual)} a year`, note: `worst fall ${hold.fall.toFixed(0)}%`, emphasis: holdingWon },
      { label: 'Technical signal', value: `${pct(signal.annual)} a year`, note: `worst fall ${signal.fall.toFixed(0)}%`, emphasis: best.name.includes('signal') },
      { label: 'Long-term trend', value: `${pct(trend.annual)} a year`, note: `worst fall ${trend.fall.toFixed(0)}%`, emphasis: best.name.includes('trend') },
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

  return {
    answer: lessReturn > 0
      ? `Spreading money between stocks, gold and cash cut the worst fall from ${stocks.maxDrawdown.toFixed(0)}% to ${mix.maxDrawdown.toFixed(0)}%, for ${lessReturn.toFixed(1)} points a year less return.`
      : `Spreading money between stocks, gold and cash earned as much as all stocks (${pct(mix.annualReturn)} a year) with a smaller worst fall (${mix.maxDrawdown.toFixed(0)}% against ${stocks.maxDrawdown.toFixed(0)}%).`,
    facts: [
      { label: 'All in stocks', value: `${pct(stocks.annualReturn)} a year`, note: `worst fall ${stocks.maxDrawdown.toFixed(0)}%` },
      { label: 'Stocks 60, gold 20, cash 20', value: `${pct(mix.annualReturn)} a year`, note: `worst fall ${mix.maxDrawdown.toFixed(0)}%`, emphasis: true },
      ...(cash ? [{ label: 'All in cash', value: `${pct(cash.annualReturn)} a year`, note: 'hardly ever falls' }] : []),
    ],
    meaning: 'If a fall like the all-stocks one would make you sell in a panic, a mix is easier to stick with. If you can sit through big falls for years, more stocks earned more. Try your own mix below.',
  }
}

/** Settings: is the data up to date? */
export function dataStatusAnswer(instruments: InstrumentDto[], now = Date.now()): PageAnswer {
  const day = 86_400_000
  const behind = instruments.filter((i) => !i.lastUpdateDate || now - new Date(i.lastUpdateDate).getTime() > 2 * day)
  const latest = instruments.map((i) => (i.lastUpdateDate ? new Date(i.lastUpdateDate).getTime() : 0)).reduce((a, b) => Math.max(a, b), 0)
  const hours = latest ? Math.max(0, Math.round((now - latest) / 3_600_000)) : null
  const last = hours == null ? 'never' : hours < 1 ? 'less than an hour ago' : hours === 1 ? '1 hour ago' : `${hours} hours ago`

  return behind.length === 0
    ? {
      answer: `Yes: all ${instruments.length} instruments are up to date. The last check was ${last}.`,
      facts: [],
      meaning: 'Nothing to do. Prices are checked every 6 hours while the app runs; use the button below if you want the latest right now.',
    }
    : {
      answer: `${behind.length} of ${instruments.length} instruments haven't updated for over 2 days: ${behind.map((i) => i.symbol).join(', ')}.`,
      facts: [],
      meaning: 'Press "Check for new data now" below. If they stay behind, their source may be down or the app may need restarting (start-app.cmd).',
    }
}
