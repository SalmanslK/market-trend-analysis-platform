import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { instrumentsApi } from '../services/api/instrumentsApi'
import { InstrumentDto, Interval, Timeframe } from '../services/types/marketData'
import IntervalSelector from '../components/controls/IntervalSelector'
import TimeframeSelector from '../components/controls/TimeframeSelector'
import { instrumentTypeLabels, intervalLabels, timeframeLabels } from '../services/labels'
import { loadPreferences, savePreferences } from '../services/preferences'

// Suggested interval/timeframe combinations for common use cases
const PRESETS: { label: string; interval: Interval; timeframe: Timeframe }[] = [
  { label: 'Short-term swing trades', interval: Interval.Daily, timeframe: Timeframe.Quarter },
  { label: 'Medium-term trend', interval: Interval.Weekly, timeframe: Timeframe.HalfYear },
  { label: 'Long-term investment view', interval: Interval.Monthly, timeframe: Timeframe.All },
  { label: 'Portfolio backtesting', interval: Interval.Quarterly, timeframe: Timeframe.All },
  { label: 'Gold or index tracking', interval: Interval.Weekly, timeframe: Timeframe.Year },
  { label: 'Mutual fund NAV trend', interval: Interval.Daily, timeframe: Timeframe.Month },
]

// The worker refreshes daily; flag sources that haven't updated in over two days
const STALE_AFTER_MS = 2 * 24 * 60 * 60 * 1000

const cellStyle = { padding: '10px', border: '1px solid #ddd' }

export default function Settings() {
  const [interval, setInterval] = useState<Interval>(() => loadPreferences().interval)
  const [timeframe, setTimeframe] = useState<Timeframe>(() => loadPreferences().timeframe)
  const [saveMessage, setSaveMessage] = useState<string | null>(null)
  const [instruments, setInstruments] = useState<InstrumentDto[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    instrumentsApi.getAll()
      .then(setInstruments)
      .catch((err) => {
        console.error('Error loading instruments:', err)
        setError('Could not load data sources.')
      })
      .finally(() => setLoading(false))
  }, [])

  const handleSave = () => {
    const saved = savePreferences({ interval, timeframe })
    setSaveMessage(saved ? 'Saved. Instrument and Comparison views will open with these defaults.' : 'Could not save: browser storage is unavailable.')
  }

  const updateStatus = (lastUpdateDate?: string) => {
    if (!lastUpdateDate) return { label: 'Never updated', color: '#d32f2f' }
    const age = Date.now() - new Date(lastUpdateDate).getTime()
    return age > STALE_AFTER_MS
      ? { label: 'Stale', color: '#f57c00' }
      : { label: 'Up to date', color: '#388e3c' }
  }

  return (
    <div style={{ padding: '20px' }}>
      <h1>Settings</h1>

      <section style={{ marginTop: '20px', padding: '20px', backgroundColor: 'white' }}>
        <h2>Default View</h2>
        <p style={{ margin: '8px 0 15px', color: '#555' }}>
          The interval and timeframe each page starts with. Saved in this browser.
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

      <section style={{ marginTop: '20px', padding: '20px', backgroundColor: 'white' }}>
        <h2>Data Sources</h2>
        <p style={{ margin: '8px 0 15px', color: '#555' }}>
          The background worker refreshes every instrument once a day. Last updated is the time of the
          most recent successful refresh.
        </p>

        {error && <div style={{ color: '#d32f2f' }}>{error}</div>}
        {loading ? (
          <div>Loading...</div>
        ) : (
          <table style={{ width: '100%', borderCollapse: 'collapse' }}>
            <thead>
              <tr style={{ backgroundColor: '#f0f0f0' }}>
                <th style={cellStyle}>Instrument</th>
                <th style={cellStyle}>Name</th>
                <th style={cellStyle}>Type</th>
                <th style={cellStyle}>Last Updated</th>
                <th style={cellStyle}>Status</th>
              </tr>
            </thead>
            <tbody>
              {instruments.map((instrument) => {
                const status = updateStatus(instrument.lastUpdateDate)
                return (
                  <tr key={instrument.id}>
                    <td style={cellStyle}>
                      <Link
                        to={`/instrument/${instrument.symbol}`}
                        style={{ color: '#1976d2', textDecoration: 'none' }}
                      >
                        {instrument.symbol}
                      </Link>
                    </td>
                    <td style={cellStyle}>{instrument.name}</td>
                    <td style={cellStyle}>{instrumentTypeLabels[instrument.type]}</td>
                    <td style={cellStyle}>
                      {instrument.lastUpdateDate ? new Date(instrument.lastUpdateDate).toLocaleString() : '-'}
                    </td>
                    <td style={{ ...cellStyle, color: status.color, fontWeight: 'bold' }}>{status.label}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        )}
      </section>
    </div>
  )
}
