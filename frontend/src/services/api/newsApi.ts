import axios from 'axios'
import { InstrumentMoves, NewsArticle, NewsSentimentSummary, NewsTopic } from '../types/marketData'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const newsApi = {
  getNews: async (params: { symbol?: string; topic?: string; days?: number; limit?: number } = {}): Promise<NewsArticle[]> => {
    const response = await api.get<NewsArticle[]>('/news', { params })
    return response.data
  },

  getTopics: async (): Promise<NewsTopic[]> => {
    const response = await api.get<NewsTopic[]>('/news/topics')
    return response.data
  },

  getSentiment: async (days = 7): Promise<NewsSentimentSummary[]> => {
    const response = await api.get<NewsSentimentSummary[]>('/news/sentiment', { params: { days } })
    return response.data
  },

  getMoves: async (symbol: string, days = 90, count = 5): Promise<InstrumentMoves> => {
    const response = await api.get<InstrumentMoves>(`/news/moves/${symbol}`, { params: { days, count } })
    return response.data
  },
}
