import axios from 'axios'
import { SignalAlert } from '../types/marketData'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const alertsApi = {
  /** Signal and trend changes in the last `days` days, newest first */
  getRecent: async (days = 30): Promise<SignalAlert[]> => {
    const response = await api.get<SignalAlert[]>('/alerts', { params: { days } })
    return response.data
  },
}
