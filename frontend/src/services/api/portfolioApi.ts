import axios from 'axios'
import { PortfolioComparison, PortfolioMix } from '../types/marketData'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const portfolioApi = {
  getPresets: async (): Promise<PortfolioMix[]> => {
    const response = await api.get<PortfolioMix[]>('/portfolio/presets')
    return response.data
  },

  /** Tests the mixes over the same dates */
  compare: async (mixes: PortfolioMix[], costPercent?: number): Promise<PortfolioComparison> => {
    const response = await api.post<PortfolioComparison>('/portfolio', mixes, { params: { costPercent } })
    return response.data
  },
}
