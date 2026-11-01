import {
  LineChart,
  Line,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  Legend,
  ResponsiveContainer,
} from 'recharts'
import { PriceDataDto } from '../../services/types/marketData'

interface PriceChartProps {
  data: PriceDataDto[]
  instrument: string
  interval: string
  timeframe: string
}

interface SeriesConfig {
  key: string
  field: keyof PriceDataDto
  color: string
  width: number
  dashed: boolean
}

// Price (black), SMA-20 (blue), Bollinger Bands (green/red dashed), then interval-specific MAs
const SERIES: SeriesConfig[] = [
  { key: 'Price', field: 'price', color: '#000000', width: 2, dashed: false },
  { key: 'SMA-20', field: 'sma20', color: '#0000FF', width: 1.5, dashed: false },
  { key: 'Upper Band', field: 'upperBand', color: '#00AA00', width: 1, dashed: true },
  { key: 'Lower Band', field: 'lowerBand', color: '#FF0000', width: 1, dashed: true },
  { key: 'MA-4W', field: 'ma4W', color: '#8884d8', width: 1, dashed: true },
  { key: 'MA-6M', field: 'ma6M', color: '#82ca9d', width: 1, dashed: true },
  { key: 'MA-12M', field: 'ma12M', color: '#ffc658', width: 1, dashed: true },
]

interface LabelProps {
  x?: number | string
  y?: number | string
  index?: number
}

export default function PriceChart({ data, instrument, interval, timeframe }: PriceChartProps) {
  // Only draw series that have at least one value in this window
  const series = SERIES.filter((s) => data.some((d) => d[s.field] != null))

  // Format data for Recharts
  const chartData = data.map((point) => ({
    date: new Date(point.dateTime).toLocaleDateString(),
    ...Object.fromEntries(series.map((s) => [s.key, point[s.field]])),
  }))

  // Get last 3 prices for info box
  const last3Prices = data.slice(-3).map((point) => ({
    date: new Date(point.dateTime).toLocaleDateString(),
    price: point.price,
  }))

  // Last non-empty value of each series, shown in the legend
  const lastValues = Object.fromEntries(series.map((s) => {
    const last = [...data].reverse().find((d) => d[s.field] != null)
    return [s.key, last ? Number(last[s.field]) : undefined]
  }))

  // Mark the last value of each line with a dot (values go in the legend, where they can't overlap)
  const lastValueDot = (s: SeriesConfig) => {
    let lastIndex = -1
    data.forEach((d, i) => {
      if (d[s.field] != null) lastIndex = i
    })

    return ({ x, y, index }: LabelProps) =>
      index === lastIndex ? <circle cx={Number(x)} cy={Number(y)} r={4} fill={s.color} /> : <g />
  }

  return (
    <div style={{ width: '100%', height: '600px', padding: '20px', backgroundColor: 'white' }}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '10px' }}>
        <h2>{instrument} | {interval} Prices ({timeframe})</h2>

        {/* Last 3 Prices Info Box */}
        <div style={{
          padding: '10px',
          backgroundColor: 'white',
          border: '1px solid black',
          borderRadius: '5px',
          fontSize: '12px',
        }}>
          <div style={{ fontWeight: 'bold', marginBottom: '5px' }}>Last Prices</div>
          {last3Prices.map((p, idx) => (
            <div key={idx}>{p.date}: {p.price.toFixed(2)}</div>
          ))}
        </div>
      </div>

      <ResponsiveContainer width="100%" height="82%">
        <LineChart data={chartData} syncId="price" margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="date" minTickGap={30} />
          {/* Fit the axis to the data so price moves are visible instead of squashed against a zero baseline */}
          <YAxis domain={['auto', 'auto']} tickFormatter={(value: number) => value.toLocaleString()} />
          <Tooltip
            formatter={(value) => (typeof value === 'number' ? value.toFixed(2) : value)}
          />
          <Legend
            formatter={(value: string) => {
              const last = lastValues[value]
              return last === undefined ? value : `${value}: ${last.toFixed(2)}`
            }}
          />

          {/* Animation is disabled: the dash-array draw-in left lines unpainted in Chrome */}
          {series.map((s) => (
            <Line
              key={s.key}
              type="monotone"
              dataKey={s.key}
              stroke={s.color}
              strokeWidth={s.width}
              strokeDasharray={s.dashed ? '5 5' : undefined}
              dot={false}
              label={lastValueDot(s)}
              isAnimationActive={false}
            />
          ))}
        </LineChart>
      </ResponsiveContainer>
    </div>
  )
}
