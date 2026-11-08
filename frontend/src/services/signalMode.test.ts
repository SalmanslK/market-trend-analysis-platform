import { describe, expect, it } from 'vitest'
import { applyBacktestMode, applySignalMode } from './signalMode'
import { BacktestResult, InstrumentAnalyticsDto } from './types/marketData'

const analytics = {
  symbol: 'KSE100',
  signal: { action: 'Sell', score: -1, maxScore: 5, reasons: [{ indicator: 'RSI (14)', impact: -1, detail: 'Overbought' }] },
  combinedSignal: { action: 'Hold', score: 0, articles: 10, newsConfidence: 60, sharedWith: [] },
  steadySignal: { action: 'Buy', score: 36, articles: 0, newsConfidence: 0, sharedWith: [] },
  steadyCombinedSignal: { action: 'Buy', score: 48, articles: 10, newsConfidence: 60, sharedWith: [] },
} as unknown as InstrumentAnalyticsDto

describe('applySignalMode', () => {
  it('shows the steady signal and keeps today\'s votes for the explanation', () => {
    const a = applySignalMode(analytics, 'steady')

    expect(a.signal).toMatchObject({ action: 'Buy', score: 36, maxScore: 100, steady: true })
    expect(a.signal.reasons).toEqual(analytics.signal.reasons)
    expect(a.combinedSignal.score).toBe(48)
    expect(a.dailySignal?.action).toBe('Sell')
  })

  it('leaves the daily signal as it is', () => {
    const a = applySignalMode(analytics, 'daily')

    expect(a.signal).toBe(analytics.signal)
    expect(a.combinedSignal).toBe(analytics.combinedSignal)
  })
})

describe('fallbacks and outcomes', () => {
  it('shows the daily signal when there is no steady reading', () => {
    const short = { ...analytics, steadySignal: null, steadyCombinedSignal: null } as InstrumentAnalyticsDto

    const a = applySignalMode(short, 'steady')

    expect(a.signal).toBe(analytics.signal)
    expect(a.combinedSignal).toBe(analytics.combinedSignal)
  })

  it('swaps the outcomes and spreads with the rules', () => {
    const performance = { totalReturn: 0, annualReturn: 0, maxDrawdown: 0, trades: 0, timeInvested: 0 }
    const backtest = {
      strategy: performance, verdict: 'DidNotHelp',
      outcomes: [{ action: 'Sell' }], steadyOutcomes: [{ action: 'Buy' }],
      buyMinusSell30D: -1, steadyBuyMinusSell30D: 2,
      rules: [{ key: 'signal', performance }, { key: 'steadySignal', performance: { ...performance, annualReturn: 9 }, verdict: 'Helped' }],
      equityCurve: [], newsTest: { rules: [], equityCurve: [] },
    } as unknown as BacktestResult

    const r = applyBacktestMode(backtest, 'steady')

    expect(r.outcomes[0].action).toBe('Buy')
    expect(r.buyMinusSell30D).toBe(2)
    expect(r.rules.map((x) => x.key)).toEqual(['signal'])
    expect(r.strategy.annualReturn).toBe(9)
  })
})
