import { NewsSentimentSummary, SentimentLabel } from './types/marketData'

export interface GroupMood {
  title: string
  symbols: string[]
  /** Stories about the group's instruments (a story about two of them counts twice) */
  articles: number
  score: number
  confidence: number
  label: SentimentLabel
}

/** Same cut-offs as each story's label (NewsSentimentAnalyzer.LabelFor) */
export function moodLabel(score: number): SentimentLabel {
  if (score >= 20) return 'Positive'
  if (score <= -20) return 'Negative'
  return 'Neutral'
}

/**
 * The combined news mood of a group of instruments: the average of their moods, each instrument counting once, so one
 * with hundreds of stories (gold) can't drown out the rest. Instruments without direct news are left out; null when
 * none has any.
 */
export function groupMood(title: string, moods: NewsSentimentSummary[]): GroupMood | null {
  const withNews = moods.filter((m) => m.articles > 0)
  const articles = withNews.reduce((sum, m) => sum + m.articles, 0)
  if (articles === 0) return null
  const weighted = (pick: (m: NewsSentimentSummary) => number) =>
    Math.round(withNews.reduce((sum, m) => sum + pick(m), 0) / withNews.length)
  const score = weighted((m) => m.score)
  return { title, symbols: withNews.map((m) => m.symbol), articles, score, confidence: weighted((m) => m.confidence), label: moodLabel(score) }
}
