import { describe, expect, it } from 'vitest'
import { groupMood, moodLabel } from './groupMood'
import { NewsSentimentSummary } from './types/marketData'

const mood = (symbol: string, articles: number, score: number, confidence = 60): NewsSentimentSummary => ({
  symbol, days: 7, articles, backgroundArticles: 0, positive: 0, neutral: 0, negative: 0, score, confidence, label: moodLabel(score),
})

describe('groupMood', () => {
  it('counts each instrument once, however many stories it has', () => {
    // 300 stories at -40 and 10 at +80: (-40 + 80) / 2 = +20
    const result = groupMood('Markets', [mood('GOLDPK', 300, -40), mood('KSE100', 10, 80)])!
    expect(result.score).toBe(20)
    expect(result.label).toBe('Positive')
    expect(result.articles).toBe(310)
    expect(result.symbols).toEqual(['GOLDPK', 'KSE100'])
  })

  it('ignores instruments without direct news and is null when none has any', () => {
    expect(groupMood('Cash', [mood('MCF', 0, 0)])).toBeNull()
    expect(groupMood('Mixed', [mood('MCF', 0, 0), mood('MIF', 5, -30)])!.label).toBe('Negative')
  })
})
