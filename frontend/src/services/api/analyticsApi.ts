import axios from 'axios'
import { InstrumentAnalyticsDto } from '../types/marketData'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const analyticsApi = {
  getAll: async (): Promise<InstrumentAnalyticsDto[]> => {
    const response = await api.get<InstrumentAnalyticsDto[]>('/analytics')
    return response.data
  },

  getBySymbol: async (symbol: string): Promise<InstrumentAnalyticsDto> => {
    const response = await api.get<InstrumentAnalyticsDto>(`/analytics/${symbol}`)
    return response.data
  },
}
