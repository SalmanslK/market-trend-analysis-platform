import { ReactNode } from 'react'
import { InstrumentAnalyticsDto } from '../../services/types/marketData'
import { changeColor, COLORS, formatNumber, formatPercent } from '../../services/format'
import RangeBar from './RangeBar'

interface KeyStatsProps {
  analytics: InstrumentAnalyticsDto
}

function Stat({ label, hint, children }: { label: string; hint?: string; children: ReactNode }) {
  return (
    <div title={hint} style={{ padding: '10px 12px', border: `1px solid ${COLORS.border}`, borderRadius: '6px' }}>
      <div style={{ fontSize: '12px', color: COLORS.muted, marginBottom: '4px' }}>{label}</div>
      <div style={{ fontSize: '16px', fontWeight: 'bold' }}>{children}</div>
    </div>
  )
}

function Change({ value }: { value?: number }) {
  return <span style={{ color: changeColor(value) }}>{formatPercent(value)}</span>
}

export default function KeyStats({ analytics: a }: KeyStatsProps) {
  const rsiNote = a.rsi14 == null ? '' : a.rsi14 > 70 ? ' (overbought)' : a.rsi14 < 30 ? ' (oversold)' : ''

  return (
    <section style={{ padding: '20px', backgroundColor: 'white', borderRadius: '6px' }}>
      <h2 style={{ fontSize: '1.2rem', marginBottom: '12px' }}>Performance &amp; Risk</h2>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(120px, 1fr))', gap: '8px' }}>
        <Stat label="1 Day"><Change value={a.change1D} /></Stat>
        <Stat label="1 Week"><Change value={a.change1W} /></Stat>
        <Stat label="1 Month"><Change value={a.change1M} /></Stat>
        <Stat label="3 Months"><Change value={a.change3M} /></Stat>
        <Stat label="Year to Date"><Change value={a.changeYtd} /></Stat>
        <Stat label="1 Year"><Change value={a.change1Y} /></Stat>
        <Stat label="Volatility (1Y)" hint="Annualized standard deviation of daily returns: how much the price typically swings in a year">
          {a.volatility1Y == null ? '-' : `${a.volatility1Y.toFixed(1)}%`}
        </Stat>
        <Stat label="Max Drawdown (1Y)" hint="Largest fall from a peak to a later low over the last year">
          <span style={{ color: changeColor(a.maxDrawdown1Y) }}>{formatPercent(a.maxDrawdown1Y, 1)}</span>
        </Stat>
        <Stat label="RSI (14)" hint="Relative Strength Index: above 70 overbought, below 30 oversold">
          {formatNumber(a.rsi14, 0)}{rsiNote}
        </Stat>
      </div>

      <div style={{ marginTop: '16px' }}>
        <div style={{ fontSize: '12px', color: COLORS.muted, marginBottom: '8px' }}>
          52-week range ({a.rangePosition52W == null ? '-' : `${a.rangePosition52W.toFixed(0)}% from low to high`})
        </div>
        <RangeBar low={a.low52W} high={a.high52W} position={a.rangePosition52W} showLabels />
      </div>
    </section>
  )
}
