import { ReactNode } from 'react'
import { InstrumentAnalyticsDto } from '../../services/types/marketData'
import { changeColor, COLORS, formatNumber, formatPercent } from '../../services/format'
import { GlossaryKey } from '../../services/glossary'
import { describeDrawdown, describeRange, describeRsi, volatilityLevel } from '../../services/plainLanguage'
import InfoTip from '../help/InfoTip'
import RangeBar from './RangeBar'

interface KeyStatsProps {
  analytics: InstrumentAnalyticsDto
}

function Stat({ label, term, note, children }: { label: string; term?: GlossaryKey; note?: string; children: ReactNode }) {
  return (
    <div style={{ padding: '10px 12px', border: `1px solid ${COLORS.border}`, borderRadius: '6px' }}>
      <div style={{ fontSize: '12px', color: COLORS.muted, marginBottom: '4px' }}>
        {label}{term && <InfoTip term={term} />}
      </div>
      <div style={{ fontSize: '16px', fontWeight: 'bold' }}>{children}</div>
      {note && <div style={{ fontSize: '12px', color: '#546e7a', marginTop: '4px', lineHeight: 1.35 }}>{note}</div>}
    </div>
  )
}

function Change({ value }: { value?: number }) {
  return <span style={{ color: changeColor(value) }}>{formatPercent(value)}</span>
}

export default function KeyStats({ analytics: a }: KeyStatsProps) {
  const volatility = volatilityLevel(a.volatility1Y)

  return (
    <section style={{ padding: '20px', backgroundColor: 'white', borderRadius: '6px' }}>
      <h2 style={{ fontSize: '1.2rem', marginBottom: '4px' }}>Performance &amp; Risk</h2>
      <p style={{ color: COLORS.muted, fontSize: '13px', marginBottom: '12px' }}>
        How much the price has changed, and how bumpy the ride has been.<InfoTip term="change" />
      </p>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(120px, 1fr))', gap: '8px' }}>
        <Stat label="1 Day"><Change value={a.change1D} /></Stat>
        <Stat label="1 Week"><Change value={a.change1W} /></Stat>
        <Stat label="1 Month"><Change value={a.change1M} /></Stat>
        <Stat label="3 Months"><Change value={a.change3M} /></Stat>
        <Stat label="This year (YTD)"><Change value={a.changeYtd} /></Stat>
        <Stat label="1 Year"><Change value={a.change1Y} /></Stat>
      </div>

      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(170px, 1fr))', gap: '8px', marginTop: '8px' }}>
        <Stat label="Volatility (1Y)" term="volatility" note={volatility && `${volatility.label}: ${volatility.text}`}>
          {a.volatility1Y == null ? '-' : `${a.volatility1Y.toFixed(1)}%`}
        </Stat>
        <Stat label="Max drawdown (1Y)" term="drawdown" note={describeDrawdown(a.maxDrawdown1Y)}>
          <span style={{ color: changeColor(a.maxDrawdown1Y) }}>{formatPercent(a.maxDrawdown1Y, 1)}</span>
        </Stat>
        <Stat label="RSI (14)" term="rsi" note={describeRsi(a.rsi14)}>
          {formatNumber(a.rsi14, 0)}
        </Stat>
      </div>

      <div style={{ marginTop: '16px' }}>
        <div style={{ fontSize: '12px', color: COLORS.muted, marginBottom: '8px' }}>
          52-week range<InfoTip term="range52w" />{' '}
          <span style={{ color: '#546e7a' }}>{describeRange(a.rangePosition52W)}</span>
        </div>
        <RangeBar low={a.low52W} high={a.high52W} position={a.rangePosition52W} showLabels />
      </div>
    </section>
  )
}
