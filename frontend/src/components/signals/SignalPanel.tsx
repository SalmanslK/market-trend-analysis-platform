import { InstrumentAnalyticsDto } from '../../services/types/marketData'
import { COLORS, formatDate, SIGNAL_DISCLAIMER } from '../../services/format'
import SignalBadge from './SignalBadge'

interface SignalPanelProps {
  analytics: InstrumentAnalyticsDto
}

const impactStyle = (impact: number) =>
  impact > 0
    ? { icon: '▲', color: COLORS.up, label: 'Bullish' }
    : impact < 0
      ? { icon: '▼', color: COLORS.down, label: 'Bearish' }
      : { icon: '●', color: COLORS.neutral, label: 'Neutral' }

export default function SignalPanel({ analytics }: SignalPanelProps) {
  const { signal } = analytics

  return (
    <section style={{ padding: '20px', backgroundColor: 'white', borderRadius: '6px' }}>
      <div style={{ display: 'flex', alignItems: 'center', gap: '12px', marginBottom: '6px' }}>
        <h2 style={{ fontSize: '1.2rem' }}>Technical Signal</h2>
        <SignalBadge signal={signal} large />
      </div>
      <p style={{ color: COLORS.muted, fontSize: '13px', marginBottom: '12px' }}>
        Each indicator adds +1 (bullish), -1 (bearish) or 0. Based on daily prices up to {formatDate(analytics.lastDate)}.
      </p>

      <ul style={{ listStyle: 'none' }}>
        {signal.reasons.map((reason) => {
          const style = impactStyle(reason.impact)
          return (
            <li
              key={reason.indicator}
              style={{ display: 'flex', gap: '10px', padding: '8px 0', borderTop: `1px solid ${COLORS.border}` }}
            >
              <span style={{ color: style.color, width: '16px' }} aria-label={style.label}>{style.icon}</span>
              <span style={{ fontWeight: 'bold', minWidth: '150px' }}>{reason.indicator}</span>
              <span>{reason.detail}</span>
            </li>
          )
        })}
      </ul>

      <p style={{ color: COLORS.muted, fontSize: '12px', marginTop: '12px' }}>{SIGNAL_DISCLAIMER}</p>
    </section>
  )
}
