import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { analyticsApi } from '../services/api/analyticsApi'
import { newsApi } from '../services/api/newsApi'
import { InstrumentAnalyticsDto, NewsSentimentSummary } from '../services/types/marketData'
import { GlossaryKey } from '../services/glossary'
import { describeMarket } from '../services/plainLanguage'
import InfoTip from '../components/help/InfoTip'
import PlainSummary from '../components/help/PlainSummary'
import SentimentChip from '../components/news/SentimentChip'
import { changeColor, COLORS, formatDate, formatNumber, formatPercent, TREND_DISCLAIMER } from '../services/format'
import TrendBadge from '../components/trend/TrendBadge'
import RangeBar from '../components/analytics/RangeBar'

type SortKey = 'symbol' | 'change1D' | 'change1M' | 'changeYtd' | 'change1Y' | 'rsi14' | 'trend' | 'news'

const TREND_SORT_ORDER = { Up: 1, Down: 0, Unknown: -1 }

const cellStyle = { padding: '10px', borderBottom: `1px solid ${COLORS.border}`, textAlign: 'right' as const }
const headerStyle = { ...cellStyle, backgroundColor: '#f5f5f5', cursor: 'pointer', userSelect: 'none' as const, whiteSpace: 'nowrap' as const }

export default function Dashboard() {
  const [analytics, setAnalytics] = useState<InstrumentAnalyticsDto[]>([])
  const [moods, setMoods] = useState<NewsSentimentSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [sortKey, setSortKey] = useState<SortKey>('symbol')
  const [sortDescending, setSortDescending] = useState(false)

  useEffect(() => {
    analyticsApi.getAll()
      .then(setAnalytics)
      .catch((err) => {
        console.error('Error loading analytics:', err)
        setError('Could not load market data. Check that the API is running.')
      })
      .finally(() => setLoading(false))
    // News mood is a bonus: the overview still works without it
    newsApi.getSentiment(7).then(setMoods).catch((err) => console.error('Error loading news sentiment:', err))
  }, [])

  const moodFor = (symbol: string) => moods.find((m) => m.symbol === symbol)

  const sorted = useMemo(() => {
    const value = (a: InstrumentAnalyticsDto): string | number => {
      if (sortKey === 'symbol') return a.symbol
      if (sortKey === 'trend') return TREND_SORT_ORDER[a.longTermTrend.direction]
      if (sortKey === 'news') return moodFor(a.symbol)?.articles ? moodFor(a.symbol)!.score : Number.NEGATIVE_INFINITY
      return a[sortKey] ?? Number.NEGATIVE_INFINITY
    }
    const rows = [...analytics].sort((x, y) => {
      const a = value(x)
      const b = value(y)
      return typeof a === 'string' ? a.localeCompare(String(b)) : a - Number(b)
    })
    return sortDescending ? rows.reverse() : rows
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [analytics, moods, sortKey, sortDescending])

  const trendsUp = analytics.filter((a) => a.longTermTrend.direction === 'Up').length
  const trendsDown = analytics.filter((a) => a.longTermTrend.direction === 'Down').length

  const latestDate = analytics.reduce<string | null>((latest, a) => (!latest || a.lastDate > latest ? a.lastDate : latest), null)

  const header = (key: SortKey, label: string, align: 'left' | 'right' | 'center' = 'right', tip?: GlossaryKey) => (
    <th
      style={{ ...headerStyle, textAlign: align }}
      onClick={() => {
        if (key === sortKey) setSortDescending(!sortDescending)
        else {
          setSortKey(key)
          // Symbols read best A-Z; numbers are most useful biggest-first
          setSortDescending(key !== 'symbol')
        }
      }}
    >
      {label}{sortKey === key ? (sortDescending ? ' ▼' : ' ▲') : ''}
      {tip && <span onClick={(e) => e.stopPropagation()}><InfoTip term={tip} /></span>}
    </th>
  )

  return (
    <div style={{ padding: '20px' }}>
      <h1>Market Overview</h1>
      <p style={{ color: COLORS.muted, margin: '6px 0 20px' }}>
        {latestDate ? `Latest prices up to ${formatDate(latestDate)}. ` : ''}
        Click a column to sort, or an instrument for its charts and news.
      </p>

      {analytics.length > 0 && (
        <PlainSummary
          points={describeMarket(analytics, moods)}
          footer={<>New here? The <Link to="/guide" style={{ color: COLORS.link }}>Guide</Link> explains every number in plain words, or hover any ⓘ.</>}
        />
      )}

      {error && <div style={{ color: COLORS.down, marginBottom: '15px' }}>{error}</div>}

      {analytics.length > 0 && (
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px', marginBottom: '20px' }}>
          <div style={{ padding: '10px 16px', borderRadius: '6px', backgroundColor: '#e6f4ea', color: '#1b5e20' }}>
            <div style={{ fontSize: '22px', fontWeight: 'bold' }}>{trendsUp}</div>
            <div style={{ fontSize: '13px' }}>Long-term trend up</div>
          </div>
          <div style={{ padding: '10px 16px', borderRadius: '6px', backgroundColor: '#fdeee9', color: '#a23a12' }}>
            <div style={{ fontSize: '22px', fontWeight: 'bold' }}>{trendsDown}</div>
            <div style={{ fontSize: '13px' }}>Long-term trend down</div>
          </div>
        </div>
      )}

      {loading ? (
        <div>Loading...</div>
      ) : (
        <div style={{ overflowX: 'auto', backgroundColor: 'white', borderRadius: '6px' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
            <thead>
              <tr>
                {header('symbol', 'Instrument', 'left')}
                <th style={{ ...headerStyle, cursor: 'default' }}>Price<InfoTip term="price" /></th>
                {header('change1D', '1D', 'right', 'change')}
                {header('change1M', '1M')}
                {header('changeYtd', 'YTD')}
                {header('change1Y', '1Y')}
                {header('rsi14', 'RSI', 'right', 'rsi')}
                <th style={{ ...headerStyle, cursor: 'default', textAlign: 'center' }}>52-Week Range<InfoTip term="range52w" /></th>
                {header('trend', 'Long-term trend', 'center', 'longTermTrend')}
                {header('news', 'News (7d)', 'center', 'newsMood')}
              </tr>
            </thead>
            <tbody>
              {sorted.map((a) => (
                <tr key={a.symbol}>
                  <td style={{ ...cellStyle, textAlign: 'left' }}>
                    <Link to={`/instrument/${a.symbol}`} style={{ color: COLORS.link, textDecoration: 'none', fontWeight: 'bold' }}>
                      {a.symbol}
                    </Link>
                    <div style={{ fontSize: '12px', color: COLORS.muted }}>{a.name}</div>
                  </td>
                  <td style={cellStyle}>
                    {formatNumber(a.lastPrice)}
                    <div style={{ fontSize: '12px', color: COLORS.muted }}>{formatDate(a.lastDate)}</div>
                  </td>
                  {[a.change1D, a.change1M, a.changeYtd, a.change1Y].map((value, i) => (
                    <td key={i} style={{ ...cellStyle, color: changeColor(value) }}>{formatPercent(value)}</td>
                  ))}
                  <td
                    style={{
                      ...cellStyle,
                      color: a.rsi14 == null ? undefined : a.rsi14 > 70 ? COLORS.down : a.rsi14 < 30 ? COLORS.up : undefined,
                    }}
                  >
                    {formatNumber(a.rsi14, 0)}
                  </td>
                  <td style={{ ...cellStyle, padding: '10px 16px' }}>
                    <RangeBar low={a.low52W} high={a.high52W} position={a.rangePosition52W} />
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    <TrendBadge trend={a.longTermTrend} />
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    {(() => {
                      const mood = moodFor(a.symbol)
                      if (!mood) return '-'
                      if (mood.articles === 0) return <span style={{ fontSize: '12px', color: COLORS.muted }}>No news</span>
                      return (
                        <Link to={`/news?symbol=${a.symbol}`} style={{ textDecoration: 'none' }} title={`${mood.articles} stories, ${mood.confidence}% sure. Click to read them.`}>
                          <SentimentChip label={mood.label} score={mood.score} compact />
                        </Link>
                      )
                    })()}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <p style={{ color: COLORS.muted, fontSize: '12px', marginTop: '12px' }}>
        {TREND_DISCLAIMER} <Link to="/track-record" style={{ color: COLORS.link }}>See how acting on it would have done in the past.</Link>
      </p>
    </div>
  )
}
