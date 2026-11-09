import { useEffect, useState, useMemo } from 'react'
import { Link, useParams, useNavigate } from 'react-router-dom'
import { instrumentsApi } from '../services/api/instrumentsApi'
import { pricesApi } from '../services/api/pricesApi'
import { analyticsApi } from '../services/api/analyticsApi'
import { newsApi } from '../services/api/newsApi'
import { InstrumentAnalyticsDto, InstrumentDto, NewsSentimentSummary, PriceDataDto, Interval, Timeframe } from '../services/types/marketData'
import PriceChart from '../components/charts/PriceChart'
import IndicatorCharts from '../components/charts/IndicatorCharts'
import InstrumentSelector from '../components/controls/InstrumentSelector'
import IntervalSelector from '../components/controls/IntervalSelector'
import TimeframeSelector from '../components/controls/TimeframeSelector'
import TrendBadge from '../components/trend/TrendBadge'
import InfoTip from '../components/help/InfoTip'
import ScaledSignalBadge from '../components/signals/ScaledSignalBadge'
import SignalPanel from '../components/signals/SignalPanel'
import AllMetrics from '../components/analytics/AllMetrics'
import KeyStats from '../components/analytics/KeyStats'
import PlainSummary from '../components/help/PlainSummary'
import InstrumentNews from '../components/news/InstrumentNews'
import { cashTestInWords, describeInstrument, signalMeaning, trendInWords } from '../services/plainLanguage'
import { intervalLabels, timeframeLabels } from '../services/labels'
import { loadPreferences } from '../services/preferences'
import { applySignalMode, useSignalMode } from '../services/signalMode'
import { useFeatures } from '../services/features'
import SignalBadge from '../components/signals/SignalBadge'
import { changeColor, COLORS, formatDate, formatNumber, formatPercent, daysSince, STALE_AFTER_DAYS, SURFACE } from '../services/format'

// The reading tiles in the answer card at the top
const tileStyle = { padding: '12px 14px', borderRadius: '10px', backgroundColor: '#f0f4f8' }
const tileLabelStyle = { fontSize: '12px', color: COLORS.muted, fontWeight: 600, textTransform: 'uppercase' as const }
const tileTextStyle = { fontSize: '14px', marginTop: '6px', color: '#334e68' }

export default function InstrumentView() {
  // The URL is the source of truth for the selected instrument, so links between instruments work
  const { symbol = '' } = useParams<{ symbol: string }>()
  const navigate = useNavigate()

  const [instruments, setInstruments] = useState<InstrumentDto[]>([])
  const [priceData, setPriceData] = useState<PriceDataDto[]>([])
  const [loaded, setAnalytics] = useState<InstrumentAnalyticsDto | null>(null)
  const mode = useSignalMode()
  const { news } = useFeatures()
  const analytics = useMemo(() => (loaded ? applySignalMode(loaded, mode, news) : null), [loaded, mode, news])
  const [mood, setMood] = useState<NewsSentimentSummary>()
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  const [interval, setInterval] = useState<Interval>(() => loadPreferences().interval)
  const [timeframe, setTimeframe] = useState<Timeframe>(() => loadPreferences().timeframe)

  useEffect(() => {
    instrumentsApi.getAll()
      .then(setInstruments)
      .catch((err) => console.error('Error loading instruments:', err))
  }, [])

  // Analytics are daily-based, so they only change with the instrument
  useEffect(() => {
    if (!symbol) return
    let cancelled = false
    setAnalytics(null)
    analyticsApi.getBySymbol(symbol)
      .then((data) => { if (!cancelled) setAnalytics(data) })
      .catch((err) => console.error('Error loading analytics:', err))
    newsApi.getSentiment(7)
      .then((moods) => { if (!cancelled) setMood(moods.find((m) => m.symbol === symbol)) })
      .catch((err) => console.error('Error loading news sentiment:', err))
    return () => { cancelled = true }
  }, [symbol])

  useEffect(() => {
    if (!symbol) return
    let cancelled = false
    setLoading(true)
    setError(null)
    pricesApi.getPriceData(symbol, interval, timeframe)
      .then((data) => { if (!cancelled) setPriceData(data) })
      .catch((err) => {
        console.error('Error loading price data:', err)
        if (!cancelled) {
          setPriceData([])
          setError(`Could not load prices for ${symbol}.`)
        }
      })
      .finally(() => { if (!cancelled) setLoading(false) })
    // Ignore a slow response for a previous selection
    return () => { cancelled = true }
  }, [symbol, interval, timeframe])

  const instrumentName = instruments.find((i) => i.symbol === symbol)?.name
  const summary = analytics ? describeInstrument(analytics, news ? mood : undefined, news) : null

  return (
    <div style={{ padding: '20px' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'center', gap: '8px', marginBottom: '8px', fontSize: '14px' }}>
        <Link to="/" style={{ color: COLORS.link }}>← Market Overview</Link>
        <label style={{ display: 'inline-flex', alignItems: 'center', justifyContent: 'flex-end', gap: '6px', color: COLORS.muted, flex: '1 1 240px', minWidth: 0, maxWidth: '100%' }}>
          Switch to
          <InstrumentSelector
            instruments={instruments}
            selectedInstrument={symbol}
            onInstrumentChange={(newSymbol) => navigate(`/instrument/${newSymbol}`)}
          />
        </label>
      </div>
      {/* The answer first: where it stands and what each reading means, before any chart or table */}
      <section style={{ ...SURFACE, padding: '20px 22px', marginBottom: '16px' }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', gap: '20px' }}>
          <div>
            <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'baseline', gap: '10px' }}>
              <h1>{symbol}</h1>
              {instrumentName && <span style={{ color: COLORS.muted, fontSize: '17px' }}>{instrumentName}</span>}
            </div>
            {analytics && (
              <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'baseline', gap: '12px', marginTop: '6px' }}>
                <span style={{ fontSize: '30px', fontWeight: 700, color: '#102a43' }}>{formatNumber(analytics.lastPrice)}</span>
                <span style={{ fontSize: '17px', color: changeColor(analytics.change1D) }}>
                  {formatPercent(analytics.change1D)} <span style={{ fontSize: '13px', color: COLORS.muted }}>today</span>
                </span>
                {daysSince(analytics.lastDate) > STALE_AFTER_DAYS ? (
                  <span style={{ color: '#e65100', fontWeight: 'bold' }}>⚠ as of {formatDate(analytics.lastDate)}, {daysSince(analytics.lastDate)} days ago</span>
                ) : (
                  <span style={{ color: COLORS.muted, fontSize: '14px' }}>as of {formatDate(analytics.lastDate)}</span>
                )}
              </div>
            )}
          </div>

          {analytics && (
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(230px, 100%), 1fr))', gap: '12px', flex: '1 1 300px', maxWidth: '640px', minWidth: 0 }}>
              {analytics.isCash ? (
                <div style={tileStyle}>
                  <div style={tileLabelStyle}>{analytics.lowRiskKind ?? 'Cash fund'}<InfoTip term="cashFund" /></div>
                  <p style={{ fontSize: '14px', marginTop: '6px' }}>Its price creeps up almost every day, so Buy/Sell signals don't apply. It shows what money earns while it waits.</p>
                </div>
              ) : (<>
                <div style={tileStyle}>
                  <div style={{ ...tileLabelStyle, marginBottom: '6px' }}>
                    {news ? 'Combined signal' : `Technical signal${analytics.signal.steady ? ', last 20 days' : ', today'}`}
                  </div>
                  {news ? <ScaledSignalBadge signal={analytics.combinedSignal} large /> : <SignalBadge signal={analytics.signal} large />}
                  {/* The meaning of the badge shown above it: the combined signal when news is on */}
                  <p style={tileTextStyle}>{signalMeaning[(news ? analytics.combinedSignal : analytics.signal).action].headline}.</p>
                </div>
                <div style={tileStyle}>
                  <div style={{ ...tileLabelStyle, marginBottom: '6px' }}>Long-term trend</div>
                  <TrendBadge trend={analytics.longTermTrend} large />
                  <p style={tileTextStyle}>{trendInWords(analytics.longTermTrend)}</p>
                </div>
                {analytics.strongerThanCash && (() => {
                  const words = cashTestInWords(analytics.symbol, analytics.strongerThanCash)
                  return (
                    // A row of its own under the signal and trend, so neither tile gets squeezed
                    <div style={{ ...tileStyle, gridColumn: '1 / -1' }}>
                      <div style={{ ...tileLabelStyle, marginBottom: '6px' }}>
                        Stronger than cash?<InfoTip term="strongerThanCash" />
                      </div>
                      <span style={{ display: 'inline-block', padding: '3px 12px', borderRadius: '999px', fontWeight: 700, fontSize: '16px', color: words.tone === 'good' ? '#1b5e20' : '#a23a12', backgroundColor: words.tone === 'good' ? '#e8f5e9' : '#fdeee9' }}>
                        {words.word}
                      </span>
                      <p style={tileTextStyle}>{words.sentence}</p>
                    </div>
                  )
                })()}
              </>)}
            </div>
          )}
        </div>
      </section>

      {summary && (
        <PlainSummary
          headline={summary.headline}
          points={summary.points}
          maxPoints={4}
          footer={`This describes what prices${news ? ' and news have' : ' have'} done so far. It isn't a prediction or financial advice.`}
        />
      )}

      {error && <div role="alert" style={{ color: COLORS.down, marginBottom: '15px' }}>{error}</div>}

      {/* The chart first: it is what most people come for. Its controls sit right above it */}
      <section aria-label="Price chart" style={{ marginBottom: '12px' }}>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px', alignItems: 'center', marginBottom: '8px', fontSize: '14px', color: COLORS.muted }}>
          <span>Chart:</span>
          <IntervalSelector selectedInterval={interval} onIntervalChange={setInterval} />
          <span>over</span>
          <TimeframeSelector selectedTimeframe={timeframe} onTimeframeChange={setTimeframe} />
          {loading && <span role="status">Loading...</span>}
        </div>
        {priceData.length > 0 ? (
          <PriceChart
            data={priceData}
            instrument={symbol}
            interval={intervalLabels[interval]}
            timeframe={timeframeLabels[timeframe]}
          />
        ) : (
          !loading && !error && <p style={{ padding: '20px', ...SURFACE, color: COLORS.muted }}>No prices for this period. Try a longer period above.</p>
        )}
      </section>

      {analytics && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(420px, 100%), 1fr))', gap: '12px', marginBottom: '12px' }}>
          {!analytics.isCash && <SignalPanel analytics={analytics} />}
          <KeyStats analytics={analytics} />
        </div>
      )}

      {news && symbol && <InstrumentNews symbol={symbol} />}

      {priceData.length > 0 && <IndicatorCharts data={priceData} />}
      {analytics && <AllMetrics analytics={analytics} />}
    </div>
  )
}
