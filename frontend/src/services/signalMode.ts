import { useSyncExternalStore } from 'react'
import { BacktestResult, InstrumentAnalyticsDto } from './types/marketData'

/**
 * Which technical signal the pages show, chosen at the top of the page and remembered in the browser:
 * steady (the daily score averaged over 20 trading days, the default because it did better in testing) or
 * daily (today's 5 votes). The combined signal, summaries, alerts and track record follow the choice.
 */
export type SignalMode = 'steady' | 'daily'

const STORAGE_KEY = 'signals.technicalMode'
const listeners = new Set<() => void>()

function read(): SignalMode {
  try {
    return localStorage.getItem(STORAGE_KEY) === 'daily' ? 'daily' : 'steady'
  } catch {
    return 'steady'
  }
}

let current: SignalMode = read()

export function setSignalMode(mode: SignalMode) {
  current = mode
  try {
    localStorage.setItem(STORAGE_KEY, mode)
  } catch {
    // Only a convenience: the choice still applies until the page is reloaded
  }
  listeners.forEach((listener) => listener())
}

export function useSignalMode(): SignalMode {
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    () => current,
  )
}

/**
 * The analytics with the chosen technical and combined signals in `signal` and `combinedSignal`, so every page and
 * summary reads the choice the same way. Today's votes stay in `dailySignal` for the explanation.
 */
export function applySignalMode(a: InstrumentAnalyticsDto, mode: SignalMode, news = true): InstrumentAnalyticsDto {
  // Too little history for a steady reading: show the daily signal rather than an empty Hold
  const chosen = mode === 'daily' || !a.steadySignal || !a.steadyCombinedSignal
    ? { ...a, dailySignal: a.signal }
    : {
      ...a,
      dailySignal: a.signal,
      signal: { action: a.steadySignal.action, score: a.steadySignal.score, maxScore: 100, reasons: a.signal.reasons, steady: true },
      combinedSignal: a.steadyCombinedSignal,
    }
  if (news) return chosen
  // News switched off: whatever acts on "the signal" (counts, sorting, holdings) uses the technical signal alone
  const score = chosen.signal.maxScore > 0 ? Math.round((chosen.signal.score / chosen.signal.maxScore) * 100) : 0
  return { ...chosen, combinedSignal: { action: chosen.signal.action, score, articles: 0, newsConfidence: 0, sharedWith: [] } }
}

/** The backtest with the chosen technical signal in the "signal", "technical" and "combined" rules, outcomes and spreads. */
export function applyBacktestMode(r: BacktestResult, mode: SignalMode): BacktestResult {
  if (mode === 'daily') return r
  const steadyRule = r.rules.find((x) => x.key === 'steadySignal')
  if (!steadyRule) return r

  const newsRule = (key: 'steadyTechnical' | 'steadyCombined', as: 'technical' | 'combined') => {
    const rule = r.newsTest.rules.find((x) => x.key === key)
    return rule ? { ...rule, key: as } : undefined
  }
  const swapped = { technical: newsRule('steadyTechnical', 'technical'), combined: newsRule('steadyCombined', 'combined') }

  return {
    ...r,
    strategy: steadyRule.performance,
    verdict: steadyRule.verdict,
    outcomes: r.steadyOutcomes ?? r.outcomes,
    buyMinusSell30D: r.steadyBuyMinusSell30D,
    buyMinusSell30DFirstHalf: r.steadyBuyMinusSell30DFirstHalf,
    buyMinusSell30DSecondHalf: r.steadyBuyMinusSell30DSecondHalf,
    rules: r.rules.filter((x) => x.key !== 'steadySignal').map((x) => (x.key === 'signal' ? { ...steadyRule, key: 'signal' } : x)),
    equityCurve: r.equityCurve.map((p) => ({ ...p, strategy: p.steadySignal })),
    newsTest: {
      ...r.newsTest,
      rules: r.newsTest.rules
        .filter((x) => x.key !== 'steadyTechnical' && x.key !== 'steadyCombined')
        .map((x) => (x.key === 'technical' && swapped.technical) || (x.key === 'combined' && swapped.combined) || x),
      equityCurve: r.newsTest.equityCurve.map((p) => ({ ...p, technical: p.steadyTechnical, combined: p.steadyCombined })),
    },
  }
}
