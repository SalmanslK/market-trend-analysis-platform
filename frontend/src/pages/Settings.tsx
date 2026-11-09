import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { instrumentsApi } from '../services/api/instrumentsApi'
import { InstrumentDto, Interval, Timeframe } from '../services/types/marketData'
import IntervalSelector from '../components/controls/IntervalSelector'
import TimeframeSelector from '../components/controls/TimeframeSelector'
import { instrumentTypeLabels, intervalLabels, timeframeLabels } from '../services/labels'
import { loadPreferences, savePreferences } from '../services/preferences'
import RefreshDataButton from '../components/layout/RefreshDataButton'
import AnswerCard from '../components/help/AnswerCard'
import { dataStatusAnswer, isPriceStale } from '../services/answers'
import { COLORS, formatDate, STALE_AFTER_DAYS, SURFACE } from '../services/format'

// Suggested interval/timeframe combinations for common use cases
const PRESETS: { label: string; interval: Interval; timeframe: Timeframe }[] = [
  { label: 'Short-term swing trades', interval: Interval.Daily, timeframe: Timeframe.Quarter },
  { label: 'Medium-term trend', interval: Interval.Weekly, timeframe: Timeframe.HalfYear },
  { label: 'Long-term investment view', interval: Interval.Monthly, timeframe: Timeframe.All },
  { label: 'Portfolio backtesting', interval: Interval.Quarterly, timeframe: Timeframe.All },
  { label: 'Gold or index tracking', interval: Interval.Weekly, timeframe: Timeframe.Year },
  { label: 'Mutual fund NAV trend', interval: Interval.Daily, timeframe: Timeframe.Month },
]

const cellStyle = { padding: '10px', border: '1px solid #ddd' }

export default function Settings() {
  const [interval, setInterval] = useState<Interval>(() => loadPreferences().interval)
  const [timeframe, setTimeframe] = useState<Timeframe>(() => loadPreferences().timeframe)
  const [saveMessage, setSaveMessage] = useState<string | null>(null)
  const [instruments, setInstruments] = useState<InstrumentDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)
  // Bumped after a manual refresh so the status table shows the new times
  const [reloadKey, setReloadKey] = useState(0)

  useEffect(() => {
    instrumentsApi.getAll()
      .then(setInstruments)
      .catch((err) => {
        console.error('Error loading instruments:', err)
        setError('Could not load data sources.')
      })
      .finally(() => setLoading(false))
  }, [reloadKey])

  const handleSave = () => {
    const saved = savePreferences({ interval, timeframe })
    setSaveMessage(saved ? 'Saved. Instrument and Comparison views will open with these defaults.' : 'Could not save: browser storage is unavailable.')
  }

  // Judged by the newest price: a check can succeed without the source having published anything new
  const updateStatus = (instrument: InstrumentDto) => {
    if (!instrument.latestPriceDate) return { label: 'No prices', color: '#d32f2f' }
    return isPriceStale(instrument)
      ? { label: `No price for over ${STALE_AFTER_DAYS} days`, color: '#b45309' }
      : { label: 'Up to date', color: COLORS.up }
  }

  return (
    <div style={{ padding: '20px' }}>
      <h1>Settings</h1>
      <p style={{ color: COLORS.muted, margin: '4px 0 16px' }}>Whether the data is up to date, and how charts open.</p>

      {error ? (
        // Without the list there is no answer, but the refresh button is exactly what's needed
        <section role="alert" style={{ padding: '16px 20px', ...SURFACE, borderLeft: `4px solid ${COLORS.down}` }}>
          <p style={{ color: COLORS.down, fontWeight: 600 }}>{error} Check that the API is running, then try again.</p>
          <div style={{ marginTop: '12px' }}>
            <RefreshDataButton onRefreshed={() => { setError(null); setReloadKey((k) => k + 1) }} />
          </div>
        </section>
      ) : !loading && (() => {
        const status = dataStatusAnswer(instruments)
        return (
          <AnswerCard question="Is the data up to date?" answer={status.answer} meaning={status.meaning}>
            <div style={{ marginTop: '14px' }}>
              <RefreshDataButton onRefreshed={() => setReloadKey((k) => k + 1)} />
            </div>
          </AnswerCard>
        )
      })()}

      <section style={{ marginTop: '20px', padding: '20px', ...SURFACE }}>
        <h2 style={{ fontSize: '1.2rem' }}>How charts open</h2>
        <p style={{ margin: '6px 0 15px', color: COLORS.muted }}>
          Pick a suggested setting, or choose your own, then save. Instrument and Compare pages will open with it. Saved in this browser.
        </p>
        <div style={{ display: 'flex', gap: '10px', alignItems: 'center' }}>
          <label style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
            Interval:
            <IntervalSelector
              selectedInterval={interval}
              onIntervalChange={(value) => { setInterval(value); setSaveMessage(null) }}
            />
          </label>
          <label style={{ display: 'flex', gap: '6px', alignItems: 'center' }}>
            Timeframe:
            <TimeframeSelector
              selectedTimeframe={timeframe}
              onTimeframeChange={(value) => { setTimeframe(value); setSaveMessage(null) }}
            />
          </label>
          <button
            onClick={handleSave}
            style={{
              padding: '8px 16px',
              fontSize: '16px',
              backgroundColor: '#1976d2',
              color: 'white',
              border: 'none',
              borderRadius: '4px',
              cursor: 'pointer',
            }}
          >
            Save
          </button>
        </div>
        {saveMessage && <p style={{ marginTop: '10px', color: '#555' }}>{saveMessage}</p>}

        <h3 style={{ marginTop: '20px' }}>Suggested settings</h3>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', marginTop: '10px' }}>
          {PRESETS.map((preset) => {
            const isCurrent = preset.interval === interval && preset.timeframe === timeframe
            return (
              <button
                key={preset.label}
                onClick={() => {
                  setInterval(preset.interval)
                  setTimeframe(preset.timeframe)
                  setSaveMessage(null)
                }}
                style={{
                  padding: '8px 12px',
                  fontSize: '14px',
                  backgroundColor: isCurrent ? '#e3f2fd' : 'white',
                  border: `1px solid ${isCurrent ? '#1976d2' : '#ccc'}`,
                  borderRadius: '4px',
                  cursor: 'pointer',
                  textAlign: 'left',
                }}
              >
                <div style={{ fontWeight: 'bold' }}>{preset.label}</div>
                <div style={{ color: '#555' }}>
                  {intervalLabels[preset.interval]}, {timeframeLabels[preset.timeframe]}
                </div>
              </button>
            )
          })}
        </div>
      </section>

      <section style={{ marginTop: '20px', padding: '20px', ...SURFACE }}>
        <h2 style={{ fontSize: '1.2rem' }}>Each instrument's last update</h2>
        <p style={{ margin: '6px 0 12px', color: COLORS.muted }}>
          The background worker checks every instrument for new prices every 6 hours while the app is running.
        </p>
        <details>
          <summary style={{ cursor: 'pointer', color: COLORS.link, fontSize: '14px', padding: '4px 0' }}>Show all {instruments.length} instruments</summary>

        {loading ? (
          <div>Loading...</div>
        ) : (
          <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ backgroundColor: '#f0f0f0' }}>
                <th style={cellStyle}>Instrument</th>
                <th style={cellStyle}>Name</th>
                <th style={cellStyle}>Type</th>
                <th style={cellStyle}>Newest price</th>
                <th style={cellStyle}>Last checked</th>
                <th style={cellStyle}>Status</th>
              </tr>
            </thead>
            <tbody>
              {instruments.map((instrument) => {
                const status = updateStatus(instrument)
                return (
                  <tr key={instrument.id}>
                    <td style={cellStyle}>
                      <Link
                        to={`/instrument/${instrument.symbol}`}
                        style={{ color: COLORS.link, textDecoration: 'none' }}
                      >
                        {instrument.symbol}
                      </Link>
                    </td>
                    <td style={cellStyle}>{instrument.name}</td>
                    <td style={cellStyle}>{instrumentTypeLabels[instrument.type]}</td>
                    <td style={cellStyle}>{instrument.latestPriceDate ? formatDate(instrument.latestPriceDate) : '-'}</td>
                    <td style={cellStyle}>
                      {instrument.lastUpdateDate ? new Date(instrument.lastUpdateDate).toLocaleString() : '-'}
                    </td>
                    <td style={{ ...cellStyle, color: status.color, fontWeight: 'bold' }}>{status.label}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
          </div>
        )}
        </details>
      </section>
    </div>
  )
}
