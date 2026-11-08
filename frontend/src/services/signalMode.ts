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
export function applySignalMode(a: InstrumentAnalyticsDto, mode: SignalMode): InstrumentAnalyticsDto {
  if (mode === 'daily' || !a.steadySignal) return { ...a, dailySignal: a.signal }
  return {
    ...a,
    dailySignal: a.signal,
    signal: { action: a.steadySignal.action, score: a.steadySignal.score, maxScore: 100, reasons: a.signal.reasons, steady: true },
    combinedSignal: a.steadyCombinedSignal,
  }
}

/** The backtest with the chosen technical signal in the "signal", "technical" and "combined" rules. */
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
