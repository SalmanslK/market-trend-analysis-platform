import { describe, expect, it } from 'vitest'
import { applySignalMode } from './signalMode'
import { InstrumentAnalyticsDto } from './types/marketData'

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
