import axios from 'axios'
import { SignalRecord } from '../types/marketData'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const signalRecordApi = {
  /** The signals saved each day since recording began, scored against what prices did over the following weeks */
  get: async (): Promise<SignalRecord> => {
    const response = await api.get<SignalRecord>('/signal-record')
    return response.data
  },
}
