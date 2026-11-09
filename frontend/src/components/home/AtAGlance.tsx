import { Link } from 'react-router-dom'
import InfoTip from '../help/InfoTip'
import { InstrumentAnalyticsDto } from '../../services/types/marketData'
import { GOLD_SYMBOLS, signedPct, trendInWords } from '../../services/plainLanguage'
import { COLORS, signalStyles, SURFACE } from '../../services/format'

interface AtAGlanceProps {
  analytics: InstrumentAnalyticsDto[]
}

type Tone = 'good' | 'mixed' | 'bad' | 'calm'

const TONES: Record<Tone, { color: string; background: string }> = {
  good: { color: '#17663a', background: '#9fd0b0' },
  mixed: { color: '#7a4f01', background: '#e9c27a' },
  bad: { color: '#a23a12', background: '#eeab98' },
  calm: { color: '#155e75', background: '#9cc6d3' },
}

/** One word for where an instrument stands: its long-term trend, and whether the price has already turned. */
function status(a: InstrumentAnalyticsDto): { word: string; tone: Tone } {
  const { direction, phase } = a.longTermTrend
  if (direction === 'Up') return phase === 'Weakening' ? { word: 'Weakening', tone: 'mixed' } : { word: 'Rising', tone: 'good' }
  if (direction === 'Down') return phase === 'Recovering' ? { word: 'Recovering', tone: 'mixed' } : { word: 'Falling', tone: 'bad' }
  return { word: 'Not enough history', tone: 'calm' }
}

/** USD/PKR rising means the rupee is weakening, so the story is told from the rupee's side. */
function rupeeStatus(change1M?: number): { word: string; tone: Tone; note: string } {
  const change = change1M ?? 0
  if (change > 0.2) return { word: 'Weakening', tone: 'bad', note: 'Imports and dollar savings cost more.' }
  if (change < -0.2) return { word: 'Firmer', tone: 'good', note: 'The rupee has gained a little against the dollar.' }
  return { word: 'Steady', tone: 'calm', note: 'The rate has hardly moved.' }
}

const pct = (value?: number) => (value == null ? '-' : signedPct(value))

/** The stronger-than-cash check in a few words, or nothing when it doesn't apply. */
const versusCash = (a: InstrumentAnalyticsDto) => {
  const test = a.strongerThanCash
  if (!test) return ''
  const untested = GOLD_SYMBOLS.includes(a.symbol) ? ' (a check tested on stocks, not gold)' : ''
  return ` Over 12 months it is ${test.aheadOfCash ? 'ahead of' : 'behind'} cash (${pct(test.return12M)} against ${pct(test.cashReturn12M)})${untested}.`
}

interface Card {
  key: string
  title: string
  word: string
  tone: Tone
  sentence: string
  signal?: string
  /** The sentence includes the stronger-than-cash check */
  versusCash?: boolean
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
    cards.push({
      key: 'stocks', title: 'Pakistan stocks', ...status(stocks),
      sentence: `The KSE-100 is ${pct(stocks.change1M)} over the past month. ${trendInWords(stocks.longTermTrend)}${versusCash(stocks)}`,
      versusCash: !!stocks.strongerThanCash,
      signal: signalStyles[stocks.signal.action].label,
      link: '/instrument/KSE100', linkText: 'Open the KSE-100',
    })
  }
  if (gold) {
    cards.push({
      key: 'gold', title: 'Gold in Pakistan', ...status(gold),
      sentence: `Gold per tola is ${pct(gold.change1M)} over the past month. ${trendInWords(gold.longTermTrend)}${versusCash(gold)}`,
      versusCash: !!gold.strongerThanCash,
      signal: signalStyles[gold.signal.action].label,
      link: '/instrument/GOLDPK', linkText: 'Open gold',
    })
  }
  if (dollar) {
    const rupee = rupeeStatus(dollar.change1M)
    cards.push({
      key: 'rupee', title: 'The rupee', word: rupee.word, tone: rupee.tone,
      sentence: `A dollar costs Rs ${dollar.lastPrice.toFixed(2)}, ${pct(dollar.change1M)} over the past month. ${rupee.note}`,
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
            <div style={{ fontSize: '15px', color: COLORS.muted, fontWeight: 700 }}>{card.title}</div>
            {/* The status in one large word, underlined in its tone: what a glance should catch first */}
            <div style={{ fontSize: '28px', fontWeight: 700, lineHeight: 1.15, color: TONES[card.tone].color, borderBottom: `4px solid ${TONES[card.tone].background}`, paddingBottom: '6px' }}>
              {card.word}
            </div>
            <p style={{ fontSize: '14px', lineHeight: 1.5, color: '#2f3d37', flex: 1 }}>
              {card.sentence}
              {card.versusCash ? <InfoTip term="strongerThanCash" /> : <InfoTip term={card.key === 'cash' ? 'cashFund' : 'change'} />}
              {card.signal && <> Technical signal: <strong>{card.signal}</strong>.<InfoTip term="signal" /></>}
            </p>
            <Link to={card.link} style={{ color: COLORS.link, fontWeight: 700, fontSize: '15px' }}>{card.linkText}</Link>
          </div>
        ))}
      </div>
    </section>
  )
}
