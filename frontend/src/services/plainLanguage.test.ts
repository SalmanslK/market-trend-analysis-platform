import { describe, expect, it } from 'vitest'
import { cashTestInWords, compareWithHolding, describeDrawdown, describeMarket, describePortfolios, describeRsi } from './plainLanguage'
import { daysSince } from './format'
import { InstrumentAnalyticsDto, PortfolioResult } from './types/marketData'

function instrument(symbol: string, overrides: Partial<InstrumentAnalyticsDto> = {}): InstrumentAnalyticsDto {
  return {
    symbol,
    name: symbol,
    type: 0,
    lastDate: '2026-11-06T00:00:00Z',
    lastPrice: 100,
    dataPoints: 500,
    change1M: 1,
    longTermTrend: { direction: 'Down' },
    signal: { action: 'Hold', score: 0, maxScore: 5, reasons: [] },
    newsSignal: { action: 'Hold', score: 0, articles: 0, newsConfidence: 0, sharedWith: [] },
    combinedSignal: { action: 'Hold', score: 0, articles: 0, newsConfidence: 0, sharedWith: [] },
    metrics: [],
    payouts: [],
    ...overrides,
  } as InstrumentAnalyticsDto
}

describe('describeMarket', () => {
  it('mentions instruments whose price has turned before their trend', () => {
    const sentences = describeMarket([
      instrument('KSE100', { longTermTrend: { direction: 'Down', phase: 'Recovering' } }),
      instrument('GOLDPK'),
    ], [])

    const trend = sentences.find((s) => s.startsWith('Long-term trend'))
    expect(trend).toContain('KSE100 already recovering')
    expect(trend).not.toContain('GOLDPK')
  })

  it('says once that instruments with the same news share one reading', () => {
    const shared = (symbol: string, others: string[]) =>
      instrument(symbol, { newsSignal: { action: 'StrongBuy', score: 60, articles: 55, newsConfidence: 79, sharedWith: others } })
    const sentences = describeMarket(
      [shared('KSE100', ['KMI30', 'MIF']), shared('KMI30', ['KSE100', 'MIF']), shared('MIF', ['KMI30', 'KSE100']), instrument('GOLDPK')],
      [],
    )

    const notes = sentences.filter((s) => s.includes('read the same news'))
    expect(notes).toEqual(['Some instruments read the same news, so their news signals move together: KMI30 & KSE100 & MIF.'])
  })

  it('explains that Hold means staying as you are', () => {
    const sentences = describeMarket([instrument('KSE100')], [])
    expect(sentences.some((s) => s.includes('Hold (stay as you are)'))).toBe(true)
  })
})

describe('compareWithHolding', () => {
  it('treats tiny differences as a tie', () => {
    expect(compareWithHolding(20.1, 20)).toBe('tied')
    expect(compareWithHolding(25, 20)).toBe('ahead')
    expect(compareWithHolding(15, 20)).toBe('behind')
  })
})

describe('plain readings', () => {
  it('reads RSI and drawdown in words', () => {
    expect(describeRsi(undefined)).toBeUndefined()
    expect(describeRsi(80)).toMatch(/overbought/i)
    expect(describeDrawdown(undefined)).toBeUndefined()
  })

  it('counts whole days since a date', () => {
    expect(daysSince(new Date(Date.now() - 3.5 * 86_400_000).toISOString())).toBe(3)
  })
})

describe('describePortfolios', () => {
  const result = (name: string, annualReturn: number, maxDrawdown: number, extra: Partial<PortfolioResult> = {}): PortfolioResult => ({
    name, annualReturn, maxDrawdown, weights: { MIF: 100 }, rebalance: 'Yearly', trendFilter: false, costPercent: 0.5,
    startDate: '2020-08-07', endDate: '2026-11-06', years: 6.2, totalReturn: 0, volatility: 10, yearlyReturns: [], trades: 1, equityCurve: [],
    ...extra,
  })

  it('names the best mix and a mix that kept most of its return with a smaller fall', () => {
    const sentences = describePortfolios({
      startDate: '2020-08-07', endDate: '2026-11-06', costPercent: 0.5, cashAnnualReturn: 12.4, instruments: [],
      results: [
        result('All in stocks', 22.4, -23.5),
        result('Stocks 60, gold 20, cash 20', 20.3, -13.1, { weights: { MIF: 60, MGF: 20, MCF: 20 } }),
        result('All in stocks, with trend filter', 20.3, -27.2, { trendFilter: true }),
      ],
    })

    expect(sentences[0]).toContain('"All in stocks" earned the most: 22.4% a year')
    expect(sentences[1]).toContain('"Stocks 60, gold 20, cash 20" earned 2.1 points a year less, but its worst fall was -13%')
    expect(sentences.some((s) => s.includes('cash fund earned 12.4%'))).toBe(true)
    expect(sentences.some((s) => s.startsWith('The trend filter on "All in stocks" hurt'))).toBe(true)
  })
})

describe('technical-only summaries', () => {
  it('leaves out news and talks about the technical signal', () => {
    const shared = instrument('KSE100', { newsSignal: { action: 'StrongBuy', score: 60, articles: 55, newsConfidence: 79, sharedWith: ['KMI30'] } })
    const sentences = describeMarket([shared, instrument('KMI30')], [], false)

    expect(sentences.some((s) => s.startsWith('Technical signal:'))).toBe(true)
    expect(sentences.some((s) => s.includes('news'))).toBe(false)
  })
})

describe('cashTestInWords', () => {
  const test = { aheadOfCash: false, return12M: 3.12, cashReturn12M: 9.86, checkedOn: '2026-11-02T00:00:00Z', since: '2026-10-01T00:00:00Z' }

  it('says behind cash with both returns and when it changed', () => {
    const words = cashTestInWords('KSE100', test)
    expect(words.word).toBe('Behind cash')
    expect(words.tone).toBe('bad')
    expect(words.sentence).toContain('+3.1%')
    expect(words.sentence).toContain('+9.9%')
    expect(words.sentence).toContain('since')
    expect(words.sentence).not.toContain('untested')
  })

  it('says ahead of cash, and that gold is untested', () => {
    const words = cashTestInWords('GOLDPK', { ...test, aheadOfCash: true, return12M: 25, since: null })
    expect(words.word).toBe('Ahead of cash')
    expect(words.sentence).toContain('stays invested')
    expect(words.sentence).not.toContain('since')
    expect(words.sentence).toContain('untested')
  })
})
