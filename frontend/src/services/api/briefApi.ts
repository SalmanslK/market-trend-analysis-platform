import axios from 'axios'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || '/api'

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

export interface DailyBrief {
  id: number
  createdAt: string
  /** "## " headings, paragraphs, "- " bullets and **bold** */
  text: string
  sources: { title: string; url: string }[]
  model: string
}

export interface BriefStatus {
  /** False when Claude Code is not available on this computer */
  canWrite: boolean
  latest?: DailyBrief | null
}

export const briefApi = {
  getLatest: async (): Promise<BriefStatus> => {
    const response = await api.get<BriefStatus>('/brief/latest')
    return response.data
  },

  /** Writing reads the news and searches the web, which takes a few minutes */
  write: async (): Promise<DailyBrief> => {
    const response = await api.post<DailyBrief>('/brief', null, { timeout: 660_000 })
    return response.data
  },
}
