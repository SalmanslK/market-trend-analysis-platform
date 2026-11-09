import { useSyncExternalStore } from 'react'
import axios from 'axios'

/**
 * Optional parts of the app, switched on in the API's appsettings.json ("Features"). News and sentiment stay hidden
 * until the API says they are on, so a technical-only setup never shows them, not even for a moment.
 */
export interface Features {
  news: boolean
}

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'
let current: Features = { news: false }
const listeners = new Set<() => void>()
let requested = false

function load() {
  if (requested) return
  requested = true
  axios.get<Features>(`${API_BASE_URL}/features`)
    .then((response) => {
      current = { news: !!response.data.news }
      listeners.forEach((listener) => listener())
    })
    .catch(() => {
      // Without an answer, the app stays technical-only
    })
}

export function useFeatures(): Features {
  load()
  return useSyncExternalStore(
    (listener) => {
      listeners.add(listener)
      return () => listeners.delete(listener)
    },
    () => current,
  )
}
