import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export interface RefreshResult {
  startedAt: string
  finishedAt: string
  instruments: { symbol: string; newPrices: number; latestDate?: string; newPayouts: number; error?: string }[]
  newStories: number
  newsError?: string
}

export const dataApi = {
  /** Fetches new prices, payouts and news now, the way the Worker does on its schedule */
  refresh: async (): Promise<RefreshResult> => {
    // Reading every source can take up to a minute
    const response = await api.post<RefreshResult>('/data/refresh', null, { timeout: 180_000 })
    return response.data
  },
}
