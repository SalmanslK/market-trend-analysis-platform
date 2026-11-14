import { describe, expect, it } from 'vitest'
import { boldParts, parseBrief } from './briefText'

describe('parseBrief', () => {
  it('splits headings, paragraphs and bullet lists', () => {
    const blocks = parseBrief('## Gold\nGold fell.\nIt is down 4%.\n\n- One\n- Two\nAfter the list.\n## Cash\nSteady.')
    expect(blocks).toEqual([
      { kind: 'heading', text: 'Gold' },
      { kind: 'paragraph', text: 'Gold fell. It is down 4%.' },
      { kind: 'list', items: ['One', 'Two'] },
      { kind: 'paragraph', text: 'After the list.' },
      { kind: 'heading', text: 'Cash' },
      { kind: 'paragraph', text: 'Steady.' },
    ])
  })

  it('treats a bullet straight after a paragraph as a new list', () => {
    expect(parseBrief('Watch:\n- Oil')).toEqual([
      { kind: 'paragraph', text: 'Watch:' },
      { kind: 'list', items: ['Oil'] },
    ])
  })
})

describe('boldParts', () => {
  it('marks the text between ** as bold', () => {
    expect(boldParts('Stocks are **up 8%** this month')).toEqual([
      { text: 'Stocks are ', bold: false },
      { text: 'up 8%', bold: true },
      { text: ' this month', bold: false },
    ])
  })
})
