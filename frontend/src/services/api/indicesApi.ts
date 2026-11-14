import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export const indicesApi = {
  /** The symbols of the companies currently in an index */
  getMembers: async (index: 'KMI30' | 'KSE100'): Promise<string[]> => {
    const response = await api.get<string[]>(`/indices/${index}/members`)
    return response.data
  },
}
