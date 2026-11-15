import { describe, expect, it } from 'vitest'
import { bigNumber, fundSizeInWords, volumeInWords } from './plainLanguage'

describe('bigNumber', () => {
  it('says millions and billions in words', () => {
    expect(bigNumber(312_400_000)).toBe('312 million')
    expect(bigNumber(59_118_390_000)).toBe('59.1 billion')
    expect(bigNumber(4_500)).toBe('4,500')
  })
})

describe('volumeInWords', () => {
  const base = { latestDate: '2026-11-13T00:00:00Z', latestShares: 1, average100: 250_000_000, busier: true }

  it('calls a month 10% or more above usual busier', () => {
    const words = volumeInWords({ ...base, average20: 312_000_000, recentVersusUsualPercent: 124.8 })
    expect(words.word).toBe('Busier than usual')
    expect(words.sentence).toBe('Over the last month about 312 million shares a day changed hands, 25% more than the usual 250 million of the last five months.')
  })

  it('treats small differences as usual', () => {
    expect(volumeInWords({ ...base, average20: 260_000_000, recentVersusUsualPercent: 104 }).word).toBe('About usual')
    expect(volumeInWords({ ...base, average20: 200_000_000, recentVersusUsualPercent: 80, busier: false }).tone).toBe('quiet')
  })
})

describe('fundSizeInWords', () => {
  it('reports size and money in or out', () => {
    const words = fundSizeInWords({ month: '2026-10-01T00:00:00Z', rupees: 59_118_390_000, flow1MPercent: -1.04, flow12MPercent: 12.4 })
    expect(words.word).toBe('Money coming in')
    expect(words.sentence).toBe('The fund held Rs 59.1 billion in October 2026. That month investors took out about 1.0% of its size. Over 12 months, money put in came to about 12% of its size a year earlier.')
  })

  it('leaves out flows it cannot work out', () => {
    const words = fundSizeInWords({ month: '2026-10-01T00:00:00Z', rupees: 7_800_000_000 })
    expect(words.word).toBe('Little change')
    expect(words.sentence).toBe('The fund held Rs 7.8 billion in October 2026.')
  })
})
