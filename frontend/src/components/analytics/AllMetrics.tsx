import { InstrumentAnalyticsDto, Metric } from '../../services/types/marketData'
import { COLORS, formatNumber, formatPercent } from '../../services/format'
import InfoTip from '../help/InfoTip'

interface AllMetricsProps {
  analytics: InstrumentAnalyticsDto
}

const GROUPS = ['Moving averages', 'Momentum', 'Volatility', 'Risk and return']

const readingColor = (reading: string) =>
  reading === 'Positive' ? COLORS.up : reading === 'Negative' ? COLORS.down : COLORS.neutral

function formatMetric(metric: Metric): string {
  if (metric.unit === 'price') return formatNumber(metric.value)
  if (metric.unit === 'percent') return metric.reading ? formatPercent(metric.value) : `${metric.value.toFixed(2)}%`
  return formatNumber(metric.value, 2)
}

function tally(metrics: Metric[]): string {
  const positive = metrics.filter((m) => m.reading === 'Positive').length
  const negative = metrics.filter((m) => m.reading === 'Negative').length
  const neutral = metrics.filter((m) => m.reading === 'Neutral').length
  return positive + negative + neutral === 0 ? '' : `${positive} positive, ${negative} negative, ${neutral} neutral`
}

/** Every standard metric that can be calculated from closing prices, grouped, each with a plain explanation. */
export default function AllMetrics({ analytics }: AllMetricsProps) {
  return (
    <section style={{ padding: '20px', backgroundColor: 'white', borderRadius: '6px', marginBottom: '12px' }}>
      <h2 style={{ fontSize: '1.2rem' }}>All metrics</h2>
      <p style={{ color: COLORS.muted, fontSize: '13px', margin: '4px 0 12px' }}>
        The standard measures used by traders and investors worldwide, on daily closing prices. Hover or tap any ⓘ for what it means.
      </p>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(480px, 100%), 1fr))', gap: '16px' }}>
        {GROUPS.map((group) => {
          const metrics = analytics.metrics.filter((m) => m.group === group)
          if (metrics.length === 0) return null
          return (
            <div key={group}>
              <h3 style={{ fontSize: '1rem' }}>{group}</h3>
              <div style={{ fontSize: '12px', color: COLORS.muted, marginBottom: '4px' }}>{tally(metrics)}</div>
              <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
                <tbody>
                  {metrics.map((metric) => (
                    <tr key={metric.name} style={{ borderTop: `1px solid ${COLORS.border}` }}>
                      <td style={{ padding: '6px 4px' }}>{metric.name}<InfoTip title={metric.name} text={metric.description} /></td>
                      <td style={{ padding: '6px 4px', textAlign: 'right', fontWeight: 'bold', whiteSpace: 'nowrap' }}>{formatMetric(metric)}</td>
                      <td style={{ padding: '6px 4px', fontSize: '12px', color: readingColor(metric.reading) }}>{metric.note}</td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )
        })}
      </div>

      <p style={{ color: COLORS.muted, fontSize: '12px', marginTop: '12px', lineHeight: 1.45 }}>
        <strong>Not shown, because the data sources don't provide what they need:</strong> ATR, ADX, Parabolic SAR and Ichimoku need each day's high and low;
        OBV, VWAP and MFI need trading volume; P/E, EPS and dividend yield need company financials, which don't apply to indices, funds, gold or currencies in the same way.
        Metrics marked "closes" normally use highs and lows, so they can differ slightly from other websites.
        Sharpe, Sortino and alpha assume cash earns 10% a year. Fund returns count payouts as reinvested; index returns leave out the dividends of the stocks in them.
      </p>
    </section>
  )
}
