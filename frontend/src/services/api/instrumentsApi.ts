import axios from 'axios'
import { InstrumentDto } from '../types/marketData'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const instrumentsApi = {
  getAll: async (): Promise<InstrumentDto[]> => {
    const response = await api.get<InstrumentDto[]>('/instruments')
    return response.data
  },

  getBySymbol: async (symbol: string): Promise<InstrumentDto> => {
    const response = await api.get<InstrumentDto>(`/instruments/${symbol}`)
    return response.data
  },
}