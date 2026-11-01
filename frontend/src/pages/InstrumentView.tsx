import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { instrumentsApi } from '../services/api/instrumentsApi'
import { pricesApi } from '../services/api/pricesApi'
import { analyticsApi } from '../services/api/analyticsApi'
import { InstrumentAnalyticsDto, InstrumentDto, PriceDataDto, Interval, Timeframe } from '../services/types/marketData'
import PriceChart from '../components/charts/PriceChart'
import IndicatorCharts from '../components/charts/IndicatorCharts'
import InstrumentSelector from '../components/controls/InstrumentSelector'
import IntervalSelector from '../components/controls/IntervalSelector'
import TimeframeSelector from '../components/controls/TimeframeSelector'
import SignalBadge from '../components/signals/SignalBadge'
import SignalPanel from '../components/signals/SignalPanel'
import KeyStats from '../components/analytics/KeyStats'
import { intervalLabels, timeframeLabels } from '../services/labels'
import { loadPreferences } from '../services/preferences'
import { changeColor, COLORS, formatDate, formatNumber, formatPercent } from '../services/format'

export default function InstrumentView() {
  // The URL is the source of truth for the selected instrument, so links between instruments work
  const { symbol = '' } = useParams<{ symbol: string }>()
  const navigate = useNavigate()

  const [instruments, setInstruments] = useState<InstrumentDto[]>([])
  const [priceData, setPriceData] = useState<PriceDataDto[]>([])
  const [analytics, setAnalytics] = useState<InstrumentAnalyticsDto | null>(null)
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

  return (
    <div style={{ padding: '20px' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'baseline', gap: '12px', marginBottom: '6px' }}>
        <h1>{symbol}</h1>
        {instrumentName && <span style={{ color: COLORS.muted, fontSize: '18px' }}>{instrumentName}</span>}
      </div>

      {analytics && (
        <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '16px', marginBottom: '20px' }}>
          <span style={{ fontSize: '28px', fontWeight: 'bold' }}>{formatNumber(analytics.lastPrice)}</span>
          <span style={{ fontSize: '18px', color: changeColor(analytics.change1D) }}>
            {formatPercent(analytics.change1D)} <span style={{ fontSize: '13px', color: COLORS.muted }}>1D</span>
          </span>
          <span style={{ color: COLORS.muted }}>as of {formatDate(analytics.lastDate)}</span>
          <SignalBadge signal={analytics.signal} large />
        </div>
      )}

      <div style={{ marginBottom: '20px', display: 'flex', flexWrap: 'wrap', gap: '10px', alignItems: 'center' }}>
        <InstrumentSelector
          instruments={instruments}
          selectedInstrument={symbol}
          onInstrumentChange={(newSymbol) => navigate(`/instrument/${newSymbol}`)}
        />
        <IntervalSelector selectedInterval={interval} onIntervalChange={setInterval} />
        <TimeframeSelector selectedTimeframe={timeframe} onTimeframeChange={setTimeframe} />
        {loading && <span style={{ color: COLORS.muted }}>Loading...</span>}
      </div>

      {error && <div style={{ color: COLORS.down, marginBottom: '15px' }}>{error}</div>}

      {analytics && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(420px, 1fr))', gap: '12px', marginBottom: '12px' }}>
          <SignalPanel analytics={analytics} />
          <KeyStats analytics={analytics} />
        </div>
      )}

      {priceData.length > 0 ? (
        <>
          <PriceChart
            data={priceData}
            instrument={symbol}
            interval={intervalLabels[interval]}
            timeframe={timeframeLabels[timeframe]}
          />
          <IndicatorCharts data={priceData} />
        </>
      ) : (
        !loading && !error && <div>No data available for this instrument and timeframe.</div>
      )}
    </div>
  )
}
