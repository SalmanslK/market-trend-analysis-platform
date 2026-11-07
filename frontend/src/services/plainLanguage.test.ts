import { describe, expect, it } from 'vitest'
import { compareWithHolding, describeDrawdown, describeMarket, describeRsi } from './plainLanguage'
import { daysSince } from './format'
import { InstrumentAnalyticsDto } from './types/marketData'

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

    const trend = sentences.find((s) => s.startsWith('The long-term trend'))
    expect(trend).toContain('KSE100 is already recovering')
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
    expect(notes).toEqual(['KMI30, KSE100, MIF read the same news, so their news signals are one reading, not 3 separate ones.'])
  })

  it('explains that Hold means staying as you are', () => {
    const sentences = describeMarket([instrument('KSE100')], [])
    expect(sentences.some((s) => s.includes('are Hold (stay as you are)'))).toBe(true)
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
