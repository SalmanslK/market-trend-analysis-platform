import axios from 'axios'
import { BacktestResult } from '../types/marketData'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const backtestApi = {
  getAll: async (costPercent?: number): Promise<BacktestResult[]> => {
    const response = await api.get<BacktestResult[]>('/backtest', { params: { costPercent } })
    return response.data
  },

  getBySymbol: async (symbol: string, costPercent?: number): Promise<BacktestResult> => {
    const response = await api.get<BacktestResult>(`/backtest/${symbol}`, { params: { costPercent } })
    return response.data
  },
}
