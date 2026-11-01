import { useEffect, useMemo, useState } from 'react'
import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ReferenceLine,
  ResponsiveContainer,
} from 'recharts'
import { instrumentsApi } from '../services/api/instrumentsApi'
import { pricesApi } from '../services/api/pricesApi'
import { InstrumentDto, PriceDataDto, Interval, Timeframe } from '../services/types/marketData'
import IntervalSelector from '../components/controls/IntervalSelector'
import TimeframeSelector from '../components/controls/TimeframeSelector'
import { intervalLabels, timeframeLabels } from '../services/labels'
import { loadPreferences } from '../services/preferences'
import PlainSummary from '../components/help/PlainSummary'

// Distinct colors for up to 9 instruments
const SERIES_COLORS = ['#1976d2', '#d32f2f', '#388e3c', '#f57c00', '#7b1fa2', '#0097a7', '#5d4037', '#c2185b', '#616161']

const DEFAULT_SELECTION = ['KSE100', 'KMI30', 'USDPKR']

interface InstrumentStats {
  symbol: string
  startDate: string
  startPrice: number
  endPrice: number
  changePercent: number
  high: number
  low: number
  points: number
}

const cellStyle = { padding: '10px', border: '1px solid #ddd' }

export default function Comparison() {
  const [instruments, setInstruments] = useState<InstrumentDto[]>([])
  const [selected, setSelected] = useState<string[]>(DEFAULT_SELECTION)
  const [interval, setInterval] = useState<Interval>(() => loadPreferences().interval)
  const [timeframe, setTimeframe] = useState<Timeframe>(() => loadPreferences().timeframe)
  const [priceData, setPriceData] = useState<Record<string, PriceDataDto[]>>({})
  const [loading, setLoading] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    instrumentsApi.getAll()
      .then(setInstruments)
      .catch((err) => {
        console.error('Error loading instruments:', err)
        setError('Could not load instruments.')
      })
  }, [])

  useEffect(() => {
    if (selected.length === 0) {
      setPriceData({})
      return
    }

    let cancelled = false
    setLoading(true)
    setError(null)

    Promise.all(selected.map(async (symbol) => [symbol, await pricesApi.getPriceData(symbol, interval, timeframe)] as const))
      .then((results) => {
        if (!cancelled) setPriceData(Object.fromEntries(results))
      })
      .catch((err) => {
        console.error('Error loading price data:', err)
        if (!cancelled) setError('Could not load price data.')
      })
      .finally(() => {
        if (!cancelled) setLoading(false)
      })

    // Ignore responses from a previous selection if the user changes it mid-request
    return () => {
      cancelled = true
    }
  }, [selected, interval, timeframe])

  const toggleInstrument = (symbol: string) => {
    setSelected((current) =>
      current.includes(symbol) ? current.filter((s) => s !== symbol) : [...current, symbol]
    )
  }

  // Keep series colors stable per instrument regardless of selection order
  const colorFor = (symbol: string) => {
    const index = instruments.findIndex((i) => i.symbol === symbol)
    return SERIES_COLORS[(index >= 0 ? index : 0) % SERIES_COLORS.length]
  }

  const { chartData, stats } = useMemo(() => {
    const rowsByDate = new Map<string, Record<string, number | string>>()
    const stats: InstrumentStats[] = []

    for (const symbol of selected) {
      const points = priceData[symbol] ?? []
      if (points.length === 0) continue

      // Normalize to % change from the first point so instruments on different scales are comparable
      const base = points[0].price
      for (const point of points) {
        const dateKey = point.dateTime.slice(0, 10)
        const row = rowsByDate.get(dateKey) ?? { dateKey }
        row[symbol] = Number((((point.price / base) - 1) * 100).toFixed(2))
        rowsByDate.set(dateKey, row)
      }

      const prices = points.map((p) => p.price)
      const endPrice = points[points.length - 1].price
      stats.push({
        symbol,
        startDate: points[0].dateTime,
        startPrice: base,
        endPrice,
        changePercent: ((endPrice / base) - 1) * 100,
        high: Math.max(...prices),
        low: Math.min(...prices),
        points: points.length,
      })
    }

    const chartData = [...rowsByDate.values()].sort((a, b) =>
      String(a.dateKey).localeCompare(String(b.dateKey))
    )

    return { chartData, stats: stats.sort((a, b) => b.changePercent - a.changePercent) }
  }, [selected, priceData])

  const formatDate = (dateKey: string) => new Date(dateKey).toLocaleDateString()

  return (
    <div style={{ padding: '20px' }}>
      <h1>Comparison View</h1>
      <p style={{ marginBottom: '20px', color: '#555' }}>
        Compare how instruments performed over the same period. Each line shows the % change from
        the first price in the selected timeframe, so instruments on very different scales share one axis.
      </p>

      <div style={{ marginBottom: '15px', display: 'flex', gap: '10px', alignItems: 'center' }}>
        <IntervalSelector selectedInterval={interval} onIntervalChange={setInterval} />
        <TimeframeSelector selectedTimeframe={timeframe} onTimeframeChange={setTimeframe} />
      </div>

      <div style={{ marginBottom: '20px', display: 'flex', flexWrap: 'wrap', gap: '8px' }}>
        {instruments.map((instrument) => {
          const isSelected = selected.includes(instrument.symbol)
          return (
            <label
              key={instrument.id}
              title={instrument.name}
              style={{
                display: 'flex',
                alignItems: 'center',
                gap: '6px',
                padding: '6px 12px',
                border: `2px solid ${isSelected ? colorFor(instrument.symbol) : '#ccc'}`,
                borderRadius: '16px',
                backgroundColor: 'white',
                cursor: 'pointer',
              }}
            >
              <input
                type="checkbox"
                checked={isSelected}
                onChange={() => toggleInstrument(instrument.symbol)}
              />
              {instrument.symbol}
            </label>
          )
        })}
      </div>

      {error && <div style={{ color: '#d32f2f', marginBottom: '15px' }}>{error}</div>}

      {stats.length >= 2 && (
        <PlainSummary
          points={[
            `Over this period (${timeframeLabels[timeframe].toLowerCase()}), ${stats[0].symbol} did best (${stats[0].changePercent >= 0 ? '+' : ''}${stats[0].changePercent.toFixed(1)}%) and ${stats[stats.length - 1].symbol} did worst (${stats[stats.length - 1].changePercent >= 0 ? '+' : ''}${stats[stats.length - 1].changePercent.toFixed(1)}%).`,
            `Lines start at 0% on the same day, so a line above another means that instrument grew more (or fell less) since then. The gap between ${stats[0].symbol} and ${stats[stats.length - 1].symbol} is ${(stats[0].changePercent - stats[stats.length - 1].changePercent).toFixed(1)} percentage points.`,
          ]}
        />
      )}

      {selected.length === 0 ? (
        <div>Select one or more instruments to compare.</div>
      ) : loading && chartData.length === 0 ? (
        <div>Loading...</div>
      ) : chartData.length === 0 ? (
        <div>No data available for the selected instruments and timeframe.</div>
      ) : (
        <>
          <div style={{ width: '100%', height: '500px', padding: '20px', backgroundColor: 'white' }}>
            <h2 style={{ marginBottom: '10px' }}>
              % Change | {intervalLabels[interval]} ({timeframeLabels[timeframe]})
            </h2>
            <ResponsiveContainer width="100%" height="88%">
              <LineChart data={chartData} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="dateKey" tickFormatter={formatDate} minTickGap={30} />
                <YAxis domain={['auto', 'auto']} tickFormatter={(value: number) => `${value}%`} />
                <Tooltip
                  labelFormatter={(label) => formatDate(String(label))}
                  formatter={(value) => (typeof value === 'number' ? `${value.toFixed(2)}%` : value)}
                />
                <Legend />
                <ReferenceLine y={0} stroke="#999" />
                {selected
                  .filter((symbol) => (priceData[symbol] ?? []).length > 0)
                  .map((symbol) => (
                    <Line
                      key={symbol}
                      type="monotone"
                      dataKey={symbol}
                      stroke={colorFor(symbol)}
                      strokeWidth={2}
                      dot={false}
                      connectNulls
                      isAnimationActive={false}
                    />
                  ))}
              </LineChart>
            </ResponsiveContainer>
          </div>

          <h2 style={{ marginTop: '20px' }}>Performance</h2>
          <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: '10px', backgroundColor: 'white' }}>
            <thead>
              <tr style={{ backgroundColor: '#f0f0f0' }}>
                <th style={cellStyle}>Instrument</th>
                <th style={cellStyle}>From</th>
                <th style={cellStyle}>Start Price</th>
                <th style={cellStyle}>Latest Price</th>
                <th style={cellStyle}>Change</th>
                <th style={cellStyle}>High</th>
                <th style={cellStyle}>Low</th>
              </tr>
            </thead>
            <tbody>
              {stats.map((row) => (
                <tr key={row.symbol}>
                  <td style={{ ...cellStyle, fontWeight: 'bold', color: colorFor(row.symbol) }}>{row.symbol}</td>
                  <td style={cellStyle}>{new Date(row.startDate).toLocaleDateString()}</td>
                  <td style={cellStyle}>{row.startPrice.toFixed(2)}</td>
                  <td style={cellStyle}>{row.endPrice.toFixed(2)}</td>
                  <td style={{ ...cellStyle, color: row.changePercent >= 0 ? '#388e3c' : '#d32f2f' }}>
                    {row.changePercent >= 0 ? '+' : ''}{row.changePercent.toFixed(2)}%
                    {row.points < 2 && ' (1 data point)'}
                  </td>
                  <td style={cellStyle}>{row.high.toFixed(2)}</td>
                  <td style={cellStyle}>{row.low.toFixed(2)}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </>
      )}
    </div>
  )
}
