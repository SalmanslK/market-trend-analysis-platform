import axios from 'axios'
import { SummaryDto, Interval, Timeframe } from '../types/marketData'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const summaryApi = {
  getSummary: async (
    interval: Interval = Interval.Daily,
    timeframe: Timeframe = Timeframe.Quarter
  ): Promise<SummaryDto[]> => {
    const response = await api.get<SummaryDto[]>('/summary', {
      params: { interval, timeframe },
    })
    return response.data
  },
}