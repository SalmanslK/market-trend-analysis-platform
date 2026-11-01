import { Interval, Timeframe } from './types/marketData'

export interface Preferences {
  interval: Interval
  timeframe: Timeframe
}

const STORAGE_KEY = 'market-analytics-preferences'

const defaults: Preferences = {
  interval: Interval.Daily,
  timeframe: Timeframe.Quarter,
}

// Storage can be unavailable (private mode, blocked site data), so fall back to defaults
export function loadPreferences(): Preferences {
  try {
    const stored = localStorage.getItem(STORAGE_KEY)
    if (!stored) return defaults
    const parsed = JSON.parse(stored) as Partial<Preferences>
    return {
      interval: parsed.interval ?? defaults.interval,
      timeframe: parsed.timeframe ?? defaults.timeframe,
    }
  } catch {
    return defaults
  }
}

export function savePreferences(preferences: Preferences): boolean {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(preferences))
    return true
  } catch {
    return false
  }
}
