import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import axios from 'axios'
import { ANSWER_SURFACE, COLORS, SURFACE } from '../services/format'
import { signedPct } from '../services/plainLanguage'

interface RankedStock {
  symbol: string
  name: string
  sector: string
  score: number
  firstYear: number
  years: number
  growthPercent: number
  indexGrowthPercent: number
  extraPercent: number
  yearsBeat: number
  yearsCounted: number
  worstFallPercent: number
}

interface Rankings {
  asOf: string
  universe: number
  eligible: number
  criteria: string[]
  industries: { name: string; eligible: number; members: number; medianScore: number | null; medianExtraPercent: number; top: RankedStock[] }[]
  final: RankedStock[]
}

const cell = { padding: '8px 10px', borderBottom: `1px solid ${COLORS.border}`, textAlign: 'right' as const, whiteSpace: 'nowrap' as const }
const head = { ...cell, color: COLORS.muted, fontWeight: 700, fontSize: '13px' }

/** One company's record in words, for the final list. */
function record(s: RankedStock): string {
  return `${signedPct(s.growthPercent)} a year since ${s.firstYear}, against ${signedPct(s.indexGrowthPercent)} for the KSE-100 over the same years. Beat the KSE-100 in ${s.yearsBeat} of ${s.yearsCounted} full years; its worst fall was ${s.worstFallPercent.toFixed(0)}%.`
}

function StockTable({ stocks, tracked }: { stocks: RankedStock[]; tracked: Set<string> }) {
  return (
    <div style={{ overflowX: 'auto' }}>
      <table style={{ borderCollapse: 'collapse', width: '100%', fontSize: '14px' }}>
        <thead>
          <tr>
            <th style={{ ...head, textAlign: 'left' }}>Company</th>
            <th style={head}>Score</th>
            <th style={head}>A year</th>
            <th style={head}>KSE-100</th>
            <th style={head}>Years ahead</th>
            <th style={head}>Worst fall</th>
          </tr>
        </thead>
        <tbody>
          {stocks.map((s) => (
            <tr key={s.symbol}>
              <td style={{ ...cell, textAlign: 'left', whiteSpace: 'normal' }}>
                {tracked.has(s.symbol)
                  ? <Link to={`/instrument/${s.symbol}`} style={{ color: COLORS.link, fontWeight: 700 }}>{s.symbol}</Link>
                  : <strong>{s.symbol}</strong>}{' '}
                <span style={{ color: COLORS.muted }}>{s.name}, since {s.firstYear}</span>
              </td>
              <td style={cell}>{s.score.toFixed(0)}</td>
              <td style={{ ...cell, color: s.growthPercent >= 0 ? COLORS.up : COLORS.down }}>{signedPct(s.growthPercent)}</td>
              <td style={cell}>{signedPct(s.indexGrowthPercent)}</td>
              <td style={cell}>{s.yearsBeat} of {s.yearsCounted}</td>
              <td style={{ ...cell, color: COLORS.down }}>{s.worstFallPercent.toFixed(0)}%</td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}

/**
 * The best past performers among Shariah-compliant PSX companies: the final ten (tracked as instruments), the best
 * industry, and the top five of every industry. Computed by scripts/research/stock-rankings.cs.
 */
export default function RankingsPage() {
  const [data, setData] = useState<Rankings | null>(null)
  const [failed, setFailed] = useState(false)

  useEffect(() => {
    axios.get<Rankings>('/data/stock-rankings.json')
      .then((r) => setData(r.data))
      .catch((err) => { console.error('Error loading rankings:', err); setFailed(true) })
  }, [])

  if (failed) return <div style={{ padding: '20px' }}><h1>Stock rankings</h1><p role="alert" style={{ color: COLORS.down }}>The rankings could not be loaded.</p></div>
  if (!data) return <div style={{ padding: '20px' }}><h1>Stock rankings</h1><p role="status">Loading...</p></div>

  const tracked = new Set(data.final.map((s) => s.symbol))
  const ranked = data.industries.filter((i) => i.medianScore != null)
  const best = ranked[0]

  return (
    <div style={{ padding: '20px', maxWidth: '960px' }}>
      <h1>Stock rankings</h1>
      <p style={{ color: COLORS.muted, marginBottom: '16px', lineHeight: 1.5 }}>
        Which Shariah-compliant companies on the Pakistan Stock Exchange have done best over their whole history, as of{' '}
        {new Date(data.asOf).toLocaleDateString(undefined, { day: 'numeric', month: 'long', year: 'numeric' })}.{' '}
        {data.eligible} of {data.universe} companies had enough history and trading to be ranked.
      </p>

      <section style={{ ...ANSWER_SURFACE, padding: '18px 22px', marginBottom: '20px' }}>
        <h2 style={{ fontSize: '1.15rem', marginBottom: '6px' }}>The ten with the best records</h2>
        <p style={{ fontSize: '14px', color: COLORS.muted, marginBottom: '10px' }}>
          At most two from any industry. Each is followed in the app like the funds: chart, signals, news and the
          stronger-than-cash check.
        </p>
        <ol style={{ paddingLeft: '22px', margin: 0 }}>
          {data.final.map((s) => (
            <li key={s.symbol} style={{ marginBottom: '10px', lineHeight: 1.5 }}>
              <Link to={`/instrument/${s.symbol}`} style={{ color: COLORS.link, fontWeight: 700 }}>{s.name}</Link>{' '}
              <span style={{ color: COLORS.muted }}>({s.symbol}, {s.sector})</span>
              <div style={{ fontSize: '14px' }}>{record(s)}</div>
            </li>
          ))}
        </ol>
      </section>

      {best && (
        <section style={{ ...SURFACE, padding: '16px 20px', marginBottom: '20px' }}>
          <h2 style={{ fontSize: '1.1rem', marginBottom: '6px' }}>Best industry: {best.name}</h2>
          <p style={{ fontSize: '14px', lineHeight: 1.5 }}>
            Its typical ranked company scored {best.medianScore?.toFixed(0)} out of 100, the highest of the{' '}
            {ranked.length} industries with at least three ranked companies. The typical company in it grew{' '}
            {Math.abs(best.medianExtraPercent).toFixed(1)} points a year {best.medianExtraPercent >= 0 ? 'more' : 'less'} than
            the KSE-100 over the same years.
            {ranked.every((i) => i.medianExtraPercent < 0) && ' In every industry the typical company trailed the index, which is weighted towards its biggest, most successful members.'}
          </p>
        </section>
      )}

      <section style={{ ...SURFACE, padding: '16px 20px', marginBottom: '20px' }}>
        <h2 style={{ fontSize: '1.1rem', marginBottom: '6px' }}>How companies were ranked</h2>
        <ul style={{ paddingLeft: '20px', fontSize: '14px', lineHeight: 1.6 }}>
          {data.criteria.map((c) => <li key={c}>{c}</li>)}
          <li>The score is 0 to 100: where a company stands among all ranked companies, averaged over the three measures.</li>
        </ul>
        <p style={{ fontSize: '14px', color: COLORS.down, marginTop: '8px', lineHeight: 1.5 }}>
          This ranks the past. Past winners don't reliably keep winning, and companies that failed or left the
          exchange aren't in today's list, which flatters every figure. Use it as a list to study, not a list to buy.
        </p>
      </section>

      <h2 style={{ fontSize: '1.15rem', margin: '24px 0 10px' }}>Top companies in each industry</h2>
      {data.industries.map((industry, i) => (
        <details key={industry.name} open={i < 3} style={{ ...SURFACE, padding: '12px 16px', marginBottom: '10px' }}>
          <summary style={{ cursor: 'pointer', fontWeight: 700 }}>
            {industry.name}{' '}
            <span style={{ fontWeight: 400, color: COLORS.muted, fontSize: '14px' }}>
              {industry.medianScore != null ? `typical score ${industry.medianScore.toFixed(0)}, ` : ''}
              {industry.eligible} of {industry.members} ranked
            </span>
          </summary>
          <div style={{ marginTop: '10px' }}><StockTable stocks={industry.top} tracked={tracked} /></div>
        </details>
      ))}
    </div>
  )
}
