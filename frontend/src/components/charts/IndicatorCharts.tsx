import {
  Bar,
  CartesianGrid,
  Cell,
  ComposedChart,
  Line,
  LineChart,
  ReferenceArea,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { PriceDataDto } from '../../services/types/marketData'
import { COLORS } from '../../services/format'

interface IndicatorChartsProps {
  data: PriceDataDto[]
}

const panelStyle = { width: '100%', height: '220px', padding: '15px 20px', backgroundColor: 'white', marginTop: '12px' }
const tooltipFormatter = (value: unknown) => (typeof value === 'number' ? value.toFixed(2) : String(value))

/** RSI and MACD panels shown under the price chart, sharing its dates. */
export default function IndicatorCharts({ data }: IndicatorChartsProps) {
  const chartData = data.map((p) => ({
    date: new Date(p.dateTime).toLocaleDateString(),
    RSI: p.rsi14,
    MACD: p.macd,
    Signal: p.macdSignal,
    Histogram: p.macdHistogram,
  }))

  const hasRsi = data.some((p) => p.rsi14 != null)
  const hasMacd = data.some((p) => p.macd != null)

  if (!hasRsi && !hasMacd) {
    return (
      <div style={{ ...panelStyle, height: 'auto', color: COLORS.muted }}>
        Not enough data points at this interval for RSI or MACD. Try a shorter interval or a longer timeframe.
      </div>
    )
  }

  return (
    <>
      {hasRsi && (
        <div style={panelStyle}>
          <h3 style={{ fontSize: '1rem' }}>
            RSI (14) <span style={{ fontWeight: 'normal', color: COLORS.muted, fontSize: '13px' }}>above 70 overbought, below 30 oversold</span>
          </h3>
          <ResponsiveContainer width="100%" height="85%">
            <LineChart data={chartData} syncId="price" margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" minTickGap={30} />
              <YAxis domain={[0, 100]} ticks={[0, 30, 50, 70, 100]} />
              <Tooltip formatter={tooltipFormatter} />
              <ReferenceArea y1={70} y2={100} fill={COLORS.down} fillOpacity={0.06} />
              <ReferenceArea y1={0} y2={30} fill={COLORS.up} fillOpacity={0.06} />
              <ReferenceLine y={70} stroke={COLORS.down} strokeDasharray="4 4" />
              <ReferenceLine y={30} stroke={COLORS.up} strokeDasharray="4 4" />
              <Line type="monotone" dataKey="RSI" stroke="#6a1b9a" strokeWidth={1.5} dot={false} isAnimationActive={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
      )}

      {hasMacd && (
        <div style={panelStyle}>
          <h3 style={{ fontSize: '1rem' }}>
            MACD (12, 26, 9) <span style={{ fontWeight: 'normal', color: COLORS.muted, fontSize: '13px' }}>bars above zero: momentum rising</span>
          </h3>
          <ResponsiveContainer width="100%" height="85%">
            <ComposedChart data={chartData} syncId="price" margin={{ top: 5, right: 30, left: 20, bottom: 5 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" minTickGap={30} />
              <YAxis domain={['auto', 'auto']} tickFormatter={(value: number) => value.toLocaleString()} />
              <Tooltip formatter={tooltipFormatter} />
              <ReferenceLine y={0} stroke="#999" />
              <Bar dataKey="Histogram" isAnimationActive={false}>
                {chartData.map((row, index) => (
                  <Cell key={index} fill={(row.Histogram ?? 0) >= 0 ? COLORS.up : COLORS.down} fillOpacity={0.6} />
                ))}
              </Bar>
              <Line type="monotone" dataKey="MACD" stroke="#1565c0" strokeWidth={1.5} dot={false} isAnimationActive={false} />
              <Line type="monotone" dataKey="Signal" stroke="#ef6c00" strokeWidth={1.5} dot={false} isAnimationActive={false} />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      )}
    </>
  )
}
