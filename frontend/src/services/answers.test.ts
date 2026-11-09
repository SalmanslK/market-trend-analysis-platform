import { describe, expect, it } from 'vitest'
import { dataStatusAnswer, portfolioAnswer, trackRecordAnswer } from './answers'
import { BacktestResult, InstrumentDto, PortfolioComparison } from './types/marketData'

const perf = (annualReturn: number, maxDrawdown: number) => ({ totalReturn: 0, annualReturn, maxDrawdown, trades: 0, timeInvested: 100 })

describe('trackRecordAnswer', () => {
  it('says holding won and that the rules cushioned falls', () => {
    const result = {
      hasEnoughData: true, years: 5, buyAndHold: perf(22, -25),
      rules: [{ key: 'signal', performance: perf(16, -18) }, { key: 'goldenCross', performance: perf(19, -22) }],
    } as unknown as BacktestResult

    const answer = trackRecordAnswer([result, { ...result, years: 1 }])!

    expect(answer.answer).toContain('simply holding earned the most: +22.0% a year')
    expect(answer.answer).toContain('smaller falls')
    expect(answer.facts[0].emphasis).toBe(true)
  })

  it('has no answer without enough history', () => {
    expect(trackRecordAnswer([{ hasEnoughData: true, years: 1 } as BacktestResult])).toBeNull()
  })

  it('names a rule that beat holding', () => {
    const result = {
      hasEnoughData: true, years: 6, buyAndHold: perf(10, -30),
      rules: [{ key: 'signal', performance: perf(8, -20) }, { key: 'goldenCross', performance: perf(14, -18) }],
    } as unknown as BacktestResult

    const answer = trackRecordAnswer([result])!

    expect(answer.answer).toContain('following the long-term trend earned the most: +14.0% a year')
    expect(answer.facts[2].emphasis).toBe(true)
  })

  it('leaves out a result missing a rule instead of counting it as 0%', () => {
    const full = {
      hasEnoughData: true, years: 5, buyAndHold: perf(20, -25),
      rules: [{ key: 'signal', performance: perf(18, -26) }, { key: 'goldenCross', performance: perf(19, -26) }],
    } as unknown as BacktestResult
    const partial = { ...full, rules: [] } as unknown as BacktestResult

    const answer = trackRecordAnswer([full, partial])!

    expect(answer.facts[1].value).toBe('+18.0% a year')
    expect(answer.answer).not.toContain('smaller falls')
  })
})

describe('portfolioAnswer', () => {
  it('states the trade-off between return and the worst fall', () => {
    const result = (name: string, annualReturn: number, maxDrawdown: number) => ({ name, annualReturn, maxDrawdown })
    const answer = portfolioAnswer({
      results: [result('All in stocks (MIF)', 22.4, -23.5), result('Stocks 60, gold 20, cash 20', 20.3, -13.1), result('All in cash (MCF)', 12.4, 0)],
    } as unknown as PortfolioComparison)!

    expect(answer.answer).toBe('Spreading money between stocks, gold and cash cut the worst fall from -24% to -13%, for 2.1 points a year less return.')
    expect(answer.facts).toHaveLength(3)
  })

  it('only claims a smaller fall when there was one', () => {
    const result = (name: string, annualReturn: number, maxDrawdown: number) => ({ name, annualReturn, maxDrawdown })
    const more = portfolioAnswer({ results: [result('All in stocks (MIF)', 10, -20), result('Stocks 60, gold 20, cash 20', 12, -25)] } as unknown as PortfolioComparison)!
    expect(more.answer).toBe('Spreading money between stocks, gold and cash earned more than all stocks (+12.0% a year against +10.0%).')
    expect(more.facts).toHaveLength(2)

    const less = portfolioAnswer({ results: [result('All in stocks (MIF)', 12, -20), result('Stocks 60, gold 20, cash 20', 10, -22)] } as unknown as PortfolioComparison)!
    expect(less.answer).toContain('without a smaller worst fall')
  })
})

describe('dataStatusAnswer', () => {
  const now = new Date('2026-11-09T12:00:00Z').getTime()
  const ago = (hours: number) => new Date(now - hours * 3_600_000).toISOString()
  const instrument = (symbol: string, priceDaysAgo?: number, checkedHoursAgo = 3) =>
    ({ symbol, lastUpdateDate: ago(checkedHoursAgo), latestPriceDate: priceDaysAgo == null ? undefined : ago(priceDaysAgo * 24) }) as InstrumentDto

  it('says all is well when every instrument has a recent price', () => {
    expect(dataStatusAnswer([instrument('KSE100', 1), instrument('MIF', 3, 5)], now).answer)
      .toBe('Yes: all 2 instruments have prices from the last few days. The last check was 3 hours ago.')
  })

  it('judges by the newest price, not by a check that found nothing new', () => {
    const answer = dataStatusAnswer([instrument('KSE100', 1), instrument('MIF', 20), instrument('MCF')], now).answer
    expect(answer).toContain('2 of 3 instruments have no price from the last 4 days')
    expect(answer).toContain('MIF (newest price')
    expect(answer).toContain('MCF (no prices)')
    expect(answer).toContain('The last check was 3 hours ago')
  })

  it('says so when there are no instruments', () => {
    expect(dataStatusAnswer([], now).answer).toBe('No instruments were found.')
  })
})
