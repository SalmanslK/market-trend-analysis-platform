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
})

describe('dataStatusAnswer', () => {
  const now = new Date('2026-11-09T12:00:00Z').getTime()
  const instrument = (symbol: string, hoursAgo?: number) =>
    ({ symbol, lastUpdateDate: hoursAgo == null ? undefined : new Date(now - hoursAgo * 3_600_000).toISOString() }) as InstrumentDto

  it('says all is well when every instrument is recent', () => {
    expect(dataStatusAnswer([instrument('KSE100', 3), instrument('MIF', 5)], now).answer).toBe('Yes: all 2 instruments are up to date. The last check was 3 hours ago.')
  })

  it('names the instruments that are behind', () => {
    expect(dataStatusAnswer([instrument('KSE100', 3), instrument('MIF', 72), instrument('MCF')], now).answer)
      .toBe("2 of 3 instruments haven't updated for over 2 days: MIF, MCF.")
  })
})
