import { useEffect, useMemo, useState } from 'react'
import { Link } from 'react-router-dom'
import { analyticsApi } from '../services/api/analyticsApi'
import { newsApi } from '../services/api/newsApi'
import { InstrumentAnalyticsDto, NewsSentimentSummary } from '../services/types/marketData'
import { GlossaryKey } from '../services/glossary'
import { describeMarket } from '../services/plainLanguage'
import InfoTip from '../components/help/InfoTip'
import Tip from '../components/help/Tip'
import PlainSummary from '../components/help/PlainSummary'
import { changeColor, COLORS, formatDate, formatNumber, formatPercent, TREND_DISCLAIMER, daysSince, STALE_AFTER_DAYS, SURFACE } from '../services/format'
import ScaledSignalBadge from '../components/signals/ScaledSignalBadge'
import SignalBadge from '../components/signals/SignalBadge'
import TrendBadge from '../components/trend/TrendBadge'
import RecentChanges from '../components/signals/RecentChanges'
import { applySignalMode, useSignalMode } from '../services/signalMode'
import RefreshDataButton from '../components/layout/RefreshDataButton'
import { useFeatures } from '../services/features'
import AtAGlance from '../components/home/AtAGlance'
import NextSteps from '../components/home/NextSteps'
import WelcomeBanner from '../components/home/WelcomeBanner'
import RangeBar from '../components/analytics/RangeBar'

type SortKey = 'symbol' | 'change1D' | 'change1M' | 'changeYtd' | 'change1Y' | 'rsi14' | 'signal' | 'combined' | 'trend' | 'news'

// Rows are grouped by what they are, so stocks, gold, the dollar and cash funds aren't mixed in one list
const GROUPS = [
  { key: 'stocks', title: 'Stocks and stock funds', note: 'Pakistan stock indices and Meezan funds that invest in shares' },
  { key: 'gold', title: 'Gold', note: 'Gold prices and the Meezan Gold Fund' },
  { key: 'currency', title: 'Currency', note: 'Rupees per US dollar: up means a weaker rupee' },
  { key: 'cash', title: 'Cash and income funds', note: "Low-risk funds whose price creeps up; Buy/Sell signals don't apply" },
] as const
type GroupKey = (typeof GROUPS)[number]['key']

function groupOf(a: InstrumentAnalyticsDto): GroupKey {
  if (a.isCash) return 'cash'
  if (a.symbol === 'USDPKR') return 'currency'
  if (a.symbol.startsWith('GOLD') || a.symbol === 'MGF') return 'gold'
  return 'stocks'
}

const MORE_COLUMNS_KEY = 'overview.moreColumns'

function readMoreColumns(): boolean {
  try {
    return localStorage.getItem(MORE_COLUMNS_KEY) === 'on'
  } catch {
    return false
  }
}

const TREND_SORT_ORDER = { Up: 1, Down: 0, Unknown: -1 }

const cellStyle = { padding: '10px', borderBottom: `1px solid ${COLORS.border}`, textAlign: 'right' as const }
const headerStyle = { ...cellStyle, backgroundColor: '#f5f5f5', cursor: 'pointer', userSelect: 'none' as const, whiteSpace: 'nowrap' as const }

export default function Dashboard() {
  const [loaded, setAnalytics] = useState<InstrumentAnalyticsDto[]>([])
  // The technical and combined signals chosen at the top of the page
  const mode = useSignalMode()
  // Technical analysis only unless news is switched on (Features:News)
  const { news } = useFeatures()
  const analytics = useMemo(() => loaded.map((a) => applySignalMode(a, mode, news)), [loaded, mode, news])
  const [moods, setMoods] = useState<NewsSentimentSummary[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [sortKey, setSortKey] = useState<SortKey>('symbol')
  const [sortDescending, setSortDescending] = useState(false)
  // Bumped after a manual refresh so the table shows the new prices and signals
  const [reloadKey, setReloadKey] = useState(0)
  // RSI is for people who know it; everyone else sees a shorter table
  const [moreColumns, setMoreColumns] = useState(readMoreColumns)
  const toggleMoreColumns = () => {
    setMoreColumns(!moreColumns)
    try {
      localStorage.setItem(MORE_COLUMNS_KEY, moreColumns ? 'off' : 'on')
    } catch {
      // Only a convenience
    }
  }

  useEffect(() => {
    analyticsApi.getAll()
      .then(setAnalytics)
      .catch((err) => {
        console.error('Error loading analytics:', err)
        setError('Could not load market data. Check that the API is running.')
      })
      .finally(() => setLoading(false))
    // News mood is a bonus: the overview still works without it
    if (news) newsApi.getSentiment(7).then(setMoods).catch((err) => console.error('Error loading news sentiment:', err))
  }, [reloadKey, news])

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

  const latestDate = analytics.reduce<string | null>((latest, a) => (!latest || a.lastDate > latest ? a.lastDate : latest), null)

  const columns = (moreColumns ? 12 : 11) - (news ? 0 : 2)

  // Column headings sort when clicked; a real button so the keyboard and screen readers can use them too
  const header = (key: SortKey, label: string, align: 'left' | 'right' | 'center' = 'right', tip?: GlossaryKey) => (
    <th
      scope="col"
      aria-sort={sortKey === key ? (sortDescending ? 'descending' : 'ascending') : 'none'}
      style={{ ...headerStyle, textAlign: align }}
    >
      <button
        type="button"
        onClick={() => {
          if (key === sortKey) setSortDescending(!sortDescending)
          else {
            setSortKey(key)
            // Symbols read best A-Z; numbers are most useful biggest-first
            setSortDescending(key !== 'symbol')
          }
        }}
        style={{ border: 'none', background: 'none', padding: 0, font: 'inherit', fontWeight: 'bold', color: 'inherit' }}
        title={`Sort by ${label}`}
      >
        {label}{sortKey === key ? (sortDescending ? ' ▼' : ' ▲') : ''}
      </button>
      {tip && <InfoTip term={tip} />}
    </th>
  )

  return (
    <div style={{ padding: '20px' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'flex-end', gap: '12px', marginBottom: '20px' }}>
        <div>
          <h1>Market Overview</h1>
          <p style={{ color: COLORS.muted, marginTop: '4px' }}>
            How Pakistan's markets are doing{latestDate ? `, with prices up to ${formatDate(latestDate)}` : ''}.
          </p>
        </div>
        <RefreshDataButton onRefreshed={() => setReloadKey((k) => k + 1)} />
      </div>

      <WelcomeBanner />

      {latestDate && daysSince(latestDate) > STALE_AFTER_DAYS && (
        <div role="alert" style={{ padding: '12px 16px', marginBottom: '16px', backgroundColor: '#fff3e0', borderLeft: '4px solid #e65100', borderRadius: '6px', lineHeight: 1.45 }}>
          <strong>Prices are {daysSince(latestDate)} days old.</strong> Try "Check for new data now" below. If prices stay old, the
          sources may be down, or the Worker may have stopped: run start-app.cmd again. Signals below are based on old prices.
        </div>
      )}

      {analytics.length > 0 && <AtAGlance analytics={analytics} />}

      <NextSteps />

      <h2 id="all-instruments" style={{ fontSize: '1.2rem', marginBottom: '4px' }}>All instruments</h2>
      <p style={{ color: COLORS.muted, fontSize: '14px', marginBottom: '12px' }}>
        Every market the app follows. Select a name for its chart and details; tap a signal to see why it says so.
      </p>

      {analytics.length > 0 && (
        <PlainSummary
          points={describeMarket(analytics, news ? moods : [], news)}
          maxPoints={3}
          footer={<>New here? The <Link to="/guide" style={{ color: COLORS.link }}>Guide</Link> explains every number in plain words, or tap any ⓘ.</>}
        />
      )}

      {error && <div style={{ color: COLORS.down, marginBottom: '15px' }}>{error}</div>}

      {loading ? (
        <div role="status" style={{ padding: '20px', color: COLORS.muted }}>Loading prices and signals...</div>
      ) : analytics.length > 0 && (
        <>
        <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: '6px' }}>
          <button
            type="button"
            aria-pressed={moreColumns}
            onClick={toggleMoreColumns}
            style={{ border: `1px solid ${COLORS.border}`, background: 'white', borderRadius: '6px', padding: '4px 10px', fontSize: '13px', color: COLORS.link }}
          >
            {moreColumns ? 'Fewer columns' : 'More columns (RSI)'}
          </button>
        </div>
        <div style={{ overflowX: 'auto', ...SURFACE }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
            <caption style={{ position: 'absolute', left: '-9999px' }}>All instruments with their recent changes and signals</caption>
            <thead>
              <tr>
                {header('symbol', 'Instrument', 'left')}
                <th scope="col" style={{ ...headerStyle, cursor: 'default' }}>Price<InfoTip term="price" /></th>
                {header('change1D', '1D', 'right', 'change')}
                {header('change1M', '1M')}
                {header('changeYtd', 'YTD')}
                {header('change1Y', '1Y')}
                {moreColumns && header('rsi14', 'RSI', 'right', 'rsi')}
                <th scope="col" style={{ ...headerStyle, cursor: 'default', textAlign: 'center' }}>52-week range<InfoTip term="range52w" /></th>
                {news && header('combined', 'Combined', 'center', 'combinedSignal')}
                {header('signal', 'Technical', 'center', 'signal')}
                {news && header('news', 'News', 'center', 'newsSignal')}
                {header('trend', 'Long-term trend', 'center', 'longTermTrend')}
              </tr>
            </thead>
            {GROUPS.map((group) => {
              const rows = sorted.filter((a) => groupOf(a) === group.key)
              if (rows.length === 0) return null
              return (
            <tbody key={group.key}>
              <tr>
                <th colSpan={columns} scope="colgroup" style={{ textAlign: 'left', padding: '14px 10px 6px', backgroundColor: '#fafafa', borderBottom: `1px solid ${COLORS.border}` }}>
                  <span style={{ fontSize: '15px' }}>{group.title}</span>
                  <span style={{ fontWeight: 'normal', fontSize: '12px', color: COLORS.muted, marginLeft: '8px' }}>{group.note}</span>
                </th>
              </tr>
              {rows.map((a) => (
                <tr key={a.symbol}>
                  <td style={{ ...cellStyle, textAlign: 'left' }}>
                    <Link to={`/instrument/${a.symbol}`} style={{ color: COLORS.link, textDecoration: 'none', fontWeight: 'bold' }}>
                      {a.symbol}
                    </Link>
                    <div style={{ fontSize: '12px', color: COLORS.muted, minWidth: '130px' }}>{a.name}</div>
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
                  {moreColumns && (
                    <td
                      style={{
                        ...cellStyle,
                        color: a.rsi14 == null || a.isCash ? undefined : a.rsi14 > 70 ? COLORS.down : a.rsi14 < 30 ? COLORS.up : undefined,
                      }}
                    >
                      {a.isCash ? '-' : formatNumber(a.rsi14, 0)}
                    </td>
                  )}
                  <td style={{ ...cellStyle, padding: '10px 16px' }}>
                    <RangeBar low={a.low52W} high={a.high52W} position={a.rangePosition52W} />
                  </td>
                  {a.isCash ? (
                    <td colSpan={news ? 4 : 2} style={{ ...cellStyle, textAlign: 'center' }}>
                      <Tip text={`A ${(a.lowRiskKind ?? 'cash fund').toLowerCase()}'s price creeps up almost every day, so Buy/Sell signals and trends don't apply. It shows what money earns while it waits; see the Portfolio page.`}>
                        <span style={{ display: 'inline-block', padding: '3px 10px', borderRadius: '12px', fontSize: '13px', backgroundColor: '#e3f2fd', color: '#0d47a1', fontWeight: 'bold' }}>
                          {a.lowRiskKind ?? 'Cash fund'} · no signals
                        </span>
                      </Tip>
                    </td>
                  ) : (<>
                  {news && (
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    <ScaledSignalBadge signal={a.combinedSignal} title="Technical and news together, from -100 to +100" />
                  </td>
                  )}
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    <SignalBadge signal={a.signal} />
                  </td>
                  {news && (
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    <Link to={`/news?symbol=${a.symbol}`} style={{ textDecoration: 'none' }}>
                      <ScaledSignalBadge
                        signal={a.newsSignal}
                        title={`${a.newsSignal.articles} stories in the last 7 days, ${a.newsSignal.newsConfidence}% sure.${a.newsSignal.sharedWith.length ? ` Same news as ${a.newsSignal.sharedWith.join(', ')}, so this is one reading, not ${a.newsSignal.sharedWith.length + 1} separate ones.` : ''} Click to read them, or open the News page.`}
                      />
                    </Link>
                  </td>
                  )}
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    <TrendBadge trend={a.longTermTrend} />
                  </td>
                  </>)}
                </tr>
              ))}
            </tbody>
              )
            })}
          </table>
        </div>
        </>
      )}

      <p style={{ color: COLORS.muted, fontSize: '12px', marginTop: '12px' }}>
        {TREND_DISCLAIMER} <Link to="/track-record" style={{ color: COLORS.link }}>See how they performed in the past.</Link>
      </p>

      <RecentChanges />
    </div>
  )
}
