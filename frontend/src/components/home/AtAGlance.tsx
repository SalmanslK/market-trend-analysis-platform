import { Link } from 'react-router-dom'
import { InstrumentAnalyticsDto } from '../../services/types/marketData'
import { trendInWords } from '../../services/plainLanguage'
import { COLORS, signalStyles, SURFACE } from '../../services/format'

interface AtAGlanceProps {
  analytics: InstrumentAnalyticsDto[]
}

type Tone = 'good' | 'mixed' | 'bad' | 'calm'

const TONES: Record<Tone, { color: string; background: string }> = {
  good: { color: '#1b5e20', background: '#e8f5e9' },
  mixed: { color: '#7a4f01', background: '#fff4dc' },
  bad: { color: '#a23a12', background: '#fdeee9' },
  calm: { color: '#0d47a1', background: '#e3f2fd' },
}

/** One word for where an instrument stands: its long-term trend, and whether the price has already turned. */
function status(a: InstrumentAnalyticsDto): { word: string; tone: Tone } {
  const { direction, phase } = a.longTermTrend
  if (direction === 'Up') return phase === 'Weakening' ? { word: 'Weakening', tone: 'mixed' } : { word: 'Rising', tone: 'good' }
  if (direction === 'Down') return phase === 'Recovering' ? { word: 'Recovering', tone: 'mixed' } : { word: 'Falling', tone: 'bad' }
  return { word: 'Not enough history', tone: 'calm' }
}

const pct = (value?: number) => (value == null ? '-' : `${value >= 0 ? '+' : ''}${value.toFixed(1)}%`)


interface Card {
  key: string
  title: string
  word: string
  tone: Tone
  sentence: string
  signal?: string
  link: string
  linkText: string
}

/**
 * The home page's first answer: in one card each, what stocks, gold, the rupee and cash are doing, in words,
 * with the way to the detail. Built only from the analytics already loaded for the table.
 */
export default function AtAGlance({ analytics }: AtAGlanceProps) {
  const find = (symbol: string) => analytics.find((a) => a.symbol === symbol)
  const stocks = find('KSE100')
  const gold = find('GOLDPK')
  const dollar = find('USDPKR')
  const cash = find('MCF')
  const cards: Card[] = []

  if (stocks) {
    const s = status(stocks)
    cards.push({
      key: 'stocks', title: 'Pakistan stocks', ...s,
      sentence: `The KSE-100 is ${pct(stocks.change1M)} this month. ${trendInWords(stocks.longTermTrend)}`,
      signal: signalStyles[stocks.signal.action].label,
      link: '/instrument/KSE100', linkText: 'Open the KSE-100',
    })
  }
  if (gold) {
    const s = status(gold)
    cards.push({
      key: 'gold', title: 'Gold in Pakistan', ...s,
      sentence: `Gold per tola is ${pct(gold.change1M)} this month. ${trendInWords(gold.longTermTrend)}`,
      signal: signalStyles[gold.signal.action].label,
      link: '/instrument/GOLDPK', linkText: 'Open gold',
    })
  }
  if (dollar) {
    // USD/PKR rising means the rupee is weakening, so the story is told from the rupee's side
    const weaker = (dollar.change1M ?? 0) > 0.2
    const firmer = (dollar.change1M ?? 0) < -0.2
    cards.push({
      key: 'rupee', title: 'The rupee',
      word: weaker ? 'Weakening' : firmer ? 'Firmer' : 'Steady',
      tone: weaker ? 'bad' : firmer ? 'good' : 'calm',
      sentence: `A dollar costs Rs ${dollar.lastPrice.toFixed(2)}, ${pct(dollar.change1M)} this month. ${weaker ? 'Imports and dollar savings cost more.' : firmer ? 'The rupee has gained a little against the dollar.' : 'The rate has hardly moved.'}`,
      link: '/instrument/USDPKR', linkText: 'Open USD/PKR',
    })
  }
  if (cash) {
    cards.push({
      key: 'cash', title: 'Cash (Meezan Cash Fund)',
      word: `${pct(cash.change1Y)} a year`, tone: 'calm',
      sentence: 'What money earned over the last 12 months while it waited, with almost no ups and downs. Compare it with stocks and gold.',
      link: '/portfolio', linkText: 'Test a mix of stocks, gold and cash',
    })
  }

  if (cards.length === 0) return null

  return (
    <section aria-labelledby="glance-title" style={{ marginBottom: '28px' }}>
      <h2 id="glance-title" style={{ fontSize: '1.2rem', marginBottom: '4px' }}>Today at a glance</h2>
      <p style={{ color: COLORS.muted, fontSize: '14px', marginBottom: '12px' }}>Where the main markets stand, in plain words.</p>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(240px, 1fr))', gap: '14px' }}>
        {cards.map((card) => (
          <div key={card.key} style={{ ...SURFACE, padding: '18px 18px 16px', display: 'flex', flexDirection: 'column', gap: '8px' }}>
            <div style={{ fontSize: '13px', color: COLORS.muted, fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.4px' }}>{card.title}</div>
            <div>
              <span style={{ display: 'inline-block', padding: '3px 12px', borderRadius: '999px', fontWeight: 700, fontSize: '16px', color: TONES[card.tone].color, backgroundColor: TONES[card.tone].background }}>
                {card.word}
              </span>
            </div>
            <p style={{ fontSize: '14px', lineHeight: 1.5, color: '#334e68', flex: 1 }}>
              {card.sentence}
              {card.signal && <> Technical signal: <strong>{card.signal}</strong>.</>}
            </p>
            <Link to={card.link} style={{ color: COLORS.link, fontWeight: 600, fontSize: '14px' }}>{card.linkText} →</Link>
          </div>
        ))}
      </div>
    </section>
  )
}
