import axios from 'axios'
import { PriceDataDto, Interval, Timeframe } from '../types/marketData'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const pricesApi = {
  getPriceData: async (
    symbol: string,
    interval: Interval = Interval.Daily,
    timeframe: Timeframe = Timeframe.Quarter
  ): Promise<PriceDataDto[]> => {
    const response = await api.get<PriceDataDto[]>(`/prices/${symbol}`, {
      params: { interval, timeframe },
    })
    return response.data
  },
}