import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { analyticsApi } from '../services/api/analyticsApi'
import { newsApi } from '../services/api/newsApi'
import { InstrumentAnalyticsDto, NewsSentimentSummary, SignalAction } from '../services/types/marketData'
import { GlossaryKey } from '../services/glossary'
import { describeMarket } from '../services/plainLanguage'
import InfoTip from '../components/help/InfoTip'
import Tip from '../components/help/Tip'
import PlainSummary from '../components/help/PlainSummary'
import { changeColor, COLORS, formatDate, formatNumber, formatPercent, signalStyles, TREND_DISCLAIMER, daysSince, STALE_AFTER_DAYS } from '../services/format'
import ScaledSignalBadge from '../components/signals/ScaledSignalBadge'
import SignalBadge from '../components/signals/SignalBadge'
import TrendBadge from '../components/trend/TrendBadge'
import RecentChanges from '../components/signals/RecentChanges'
import { applySignalMode, useSignalMode } from '../services/signalMode'
import RangeBar from '../components/analytics/RangeBar'

type SortKey = 'symbol' | 'change1D' | 'change1M' | 'changeYtd' | 'change1Y' | 'rsi14' | 'signal' | 'combined' | 'trend' | 'news'

const SIGNAL_ORDER: SignalAction[] = ['StrongBuy', 'Buy', 'Hold', 'Sell', 'StrongSell']

const TREND_SORT_ORDER = { Up: 1, Down: 0, Unknown: -1 }

const cellStyle = { padding: '10px', borderBottom: `1px solid ${COLORS.border}`, textAlign: 'right' as const }
const headerStyle = { ...cellStyle, backgroundColor: '#f5f5f5', cursor: 'pointer', userSelect: 'none' as const, whiteSpace: 'nowrap' as const }

export default function Dashboard() {
  const [loaded, setAnalytics] = useState<InstrumentAnalyticsDto[]>([])
  // The technical and combined signals chosen at the top of the page
  const mode = useSignalMode()
  const analytics = useMemo(() => loaded.map((a) => applySignalMode(a, mode)), [loaded, mode])
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

  const sorted = useMemo(() => {
    const value = (a: InstrumentAnalyticsDto): string | number => {
      if (sortKey === 'symbol') return a.symbol
      if (sortKey === 'signal') return a.signal.score
      if (sortKey === 'trend') return TREND_SORT_ORDER[a.longTermTrend.direction]
      if (sortKey === 'combined') return a.combinedSignal.score
      if (sortKey === 'news') return a.newsSignal.score
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

  const signalCounts = SIGNAL_ORDER
    .map((action) => ({ action, count: analytics.filter((a) => !a.isCash && a.combinedSignal.action === action).length }))
    .filter((s) => s.count > 0)

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
        Click a column to sort, an instrument for its charts and news, or hover or tap a signal to see why.
      </p>

      {latestDate && daysSince(latestDate) > STALE_AFTER_DAYS && (
        <div role="alert" style={{ padding: '12px 16px', marginBottom: '16px', backgroundColor: '#fff3e0', borderLeft: '4px solid #e65100', borderRadius: '6px', lineHeight: 1.45 }}>
          <strong>Prices are {daysSince(latestDate)} days old.</strong> The Worker that fetches new prices may not be running.
          Start the app with start-app.cmd, or see the logs folder. Signals below are based on old prices.
        </div>
      )}

      {analytics.length > 0 && (
        <PlainSummary
          points={describeMarket(analytics, moods)}
          footer={<>New here? The <Link to="/guide" style={{ color: COLORS.link }}>Guide</Link> explains every number in plain words, or tap any ⓘ.</>}
        />
      )}

      {error && <div style={{ color: COLORS.down, marginBottom: '15px' }}>{error}</div>}

      {signalCounts.length > 0 && (
        <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '10px', marginBottom: '20px' }}>
          <span style={{ fontSize: '13px', color: COLORS.muted }}>Combined signals:</span>
          {signalCounts.map(({ action, count }) => (
            <div
              key={action}
              style={{
                padding: '10px 16px',
                borderRadius: '6px',
                backgroundColor: signalStyles[action].background,
                color: signalStyles[action].color,
              }}
            >
              <div style={{ fontSize: '22px', fontWeight: 'bold' }}>{count}</div>
              <div style={{ fontSize: '13px' }}>{signalStyles[action].label}</div>
            </div>
          ))}
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
                {header('combined', 'Combined', 'center', 'combinedSignal')}
                {header('signal', 'Technical', 'center', 'signal')}
                {header('news', 'News', 'center', 'newsSignal')}
                {header('trend', 'Long-term trend', 'center', 'longTermTrend')}
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
                    {latestDate && daysSince(a.lastDate) - daysSince(latestDate) > STALE_AFTER_DAYS + 1 ? (
                      <Tip text={`No new price since ${formatDate(a.lastDate)}, while others are updated to ${formatDate(latestDate)}. Its source may have stopped publishing.`}>
                        <div style={{ fontSize: '12px', color: '#e65100', fontWeight: 'bold' }}>⚠ {formatDate(a.lastDate)}</div>
                      </Tip>
                    ) : (
                      <div style={{ fontSize: '12px', color: COLORS.muted }}>{formatDate(a.lastDate)}</div>
                    )}
                  </td>
                  {[a.change1D, a.change1M, a.changeYtd, a.change1Y].map((value, i) => (
                    <td key={i} style={{ ...cellStyle, color: changeColor(value) }}>{formatPercent(value)}</td>
                  ))}
                  <td
                    style={{
                      ...cellStyle,
                      color: a.rsi14 == null || a.isCash ? undefined : a.rsi14 > 70 ? COLORS.down : a.rsi14 < 30 ? COLORS.up : undefined,
                    }}
                  >
                    {a.isCash ? '-' : formatNumber(a.rsi14, 0)}
                  </td>
                  <td style={{ ...cellStyle, padding: '10px 16px' }}>
                    <RangeBar low={a.low52W} high={a.high52W} position={a.rangePosition52W} />
                  </td>
                  {a.isCash ? (
                    <td colSpan={4} style={{ ...cellStyle, textAlign: 'center' }}>
                      <Tip text="A cash fund's price creeps up almost every day, so Buy/Sell signals and trends don't apply. It shows what money earns while it waits; see the Portfolio page.">
                        <span style={{ display: 'inline-block', padding: '3px 10px', borderRadius: '12px', fontSize: '13px', backgroundColor: '#e3f2fd', color: '#0d47a1', fontWeight: 'bold' }}>
                          Cash fund · no signals
                        </span>
                      </Tip>
                    </td>
                  ) : (<>
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    <ScaledSignalBadge signal={a.combinedSignal} title="Technical and news together, from -100 to +100" />
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    <SignalBadge signal={a.signal} />
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    <Link to={`/news?symbol=${a.symbol}`} style={{ textDecoration: 'none' }}>
                      <ScaledSignalBadge
                        signal={a.newsSignal}
                        title={`${a.newsSignal.articles} stories in the last 7 days, ${a.newsSignal.newsConfidence}% sure.${a.newsSignal.sharedWith.length ? ` Same news as ${a.newsSignal.sharedWith.join(', ')}, so this is one reading, not ${a.newsSignal.sharedWith.length + 1} separate ones.` : ''} Click to read them, or open the News page.`}
                      />
                    </Link>
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    <TrendBadge trend={a.longTermTrend} />
                  </td>
                  </>)}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}

      <p style={{ color: COLORS.muted, fontSize: '12px', marginTop: '12px' }}>
        {TREND_DISCLAIMER} <Link to="/track-record" style={{ color: COLORS.link }}>See how they performed in the past.</Link>
      </p>

      <RecentChanges />
    </div>
  )
}
