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

export default function PriceChart({ data, instrument, interval, timeframe }: PriceChartProps) {
  // Format data for Recharts
  const chartData = data.map((point) => ({
    date: new Date(point.dateTime).toLocaleDateString(),
    dateTime: point.dateTime,
    Price: point.price,
    'SMA-20': point.sma20,
    'Upper Band': point.upperBand,
    'Lower Band': point.lowerBand,
    'MA-4W': point.ma4W,
    'MA-6M': point.ma6M,
    'MA-12M': point.ma12M,
  }))

  // Get last 3 prices for info box
  const last3Prices = data.slice(-3).map((point) => ({
    date: new Date(point.dateTime).toLocaleDateString(),
    price: point.price,
  }))

  return (
    <div style={{ width: '100%', height: '600px', padding: '20px', backgroundColor: 'white' }}>
      <h2>{instrument} | {interval} Prices ({timeframe})</h2>
      
      <ResponsiveContainer width="100%" height="90%">
        <LineChart data={chartData} margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
          <CartesianGrid strokeDasharray="3 3" />
          <XAxis dataKey="date" />
          <YAxis />
          <Tooltip />
          <Legend />
          
          {/* Price line (black) */}
          <Line
            type="monotone"
            dataKey="Price"
            stroke="#000000"
            strokeWidth={2}
            dot={false}
          />
          
          {/* SMA-20 line (blue) */}
          {data.some(d => d.sma20) && (
            <Line
              type="monotone"
              dataKey="SMA-20"
              stroke="#0000FF"
              strokeWidth={1.5}
              dot={false}
            />
          )}
          
          {/* Bollinger Bands (green/red dashed) */}
          {data.some(d => d.upperBand) && (
            <Line
              type="monotone"
              dataKey="Upper Band"
              stroke="#00FF00"
              strokeWidth={1}
              strokeDasharray="5 5"
              dot={false}
            />
          )}
          
          {data.some(d => d.lowerBand) && (
            <Line
              type="monotone"
              dataKey="Lower Band"
              stroke="#FF0000"
              strokeWidth={1}
              strokeDasharray="5 5"
              dot={false}
            />
          )}
          
          {/* Interval-specific MAs */}
          {data.some(d => d.ma4W) && (
            <Line
              type="monotone"
              dataKey="MA-4W"
              stroke="#8884d8"
              strokeWidth={1}
              strokeDasharray="5 5"
              dot={false}
            />
          )}
          
          {data.some(d => d.ma6M) && (
            <Line
              type="monotone"
              dataKey="MA-6M"
              stroke="#82ca9d"
              strokeWidth={1}
              strokeDasharray="5 5"
              dot={false}
            />
          )}
          
          {data.some(d => d.ma12M) && (
            <Line
              type="monotone"
              dataKey="MA-12M"
              stroke="#ffc658"
              strokeWidth={1}
              strokeDasharray="5 5"
              dot={false}
            />
          )}
        </LineChart>
      </ResponsiveContainer>
      
      {/* Last 3 Prices Info Box */}
      <div style={{
        position: 'absolute',
        bottom: '30px',
        right: '30px',
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
  )
}