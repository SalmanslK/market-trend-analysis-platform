import { InstrumentAnalyticsDto } from '../../services/types/marketData'
import { COLORS, formatDate, TREND_DISCLAIMER } from '../../services/format'
import { describeTrend, directionNote } from '../../services/plainLanguage'
import InfoTip from '../help/InfoTip'
import TrendBadge from './TrendBadge'
import TrendTrackRecord from './TrendTrackRecord'

interface TrendPanelProps {
  analytics: InstrumentAnalyticsDto
}

const impactStyle = (impact: number) =>
  impact > 0
    ? { icon: '▲', color: COLORS.up, label: 'Points up' }
    : impact < 0
      ? { icon: '▼', color: COLORS.down, label: 'Points down' }
      : { icon: '●', color: COLORS.neutral, label: 'No view' }

export default function TrendPanel({ analytics }: TrendPanelProps) {
  const { signal, longTermTrend } = analytics
  const up = signal.reasons.filter((r) => r.impact > 0).length
  const down = signal.reasons.filter((r) => r.impact < 0).length
  const none = signal.reasons.length - up - down
  const note = directionNote(analytics.symbol)

  return (
    <section style={{ padding: '20px', backgroundColor: 'white', borderRadius: '6px' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '12px', marginBottom: '6px' }}>
        <h2 style={{ fontSize: '1.2rem' }}>Long-term trend<InfoTip term="longTermTrend" /></h2>
        <TrendBadge trend={longTermTrend} large />
      </div>
      <p style={{ lineHeight: 1.45 }}>{describeTrend(analytics)}</p>
      {note && <p style={{ lineHeight: 1.45, marginTop: '6px', fontSize: '13px', color: '#546e7a' }}>{note}</p>}

      <TrendTrackRecord symbol={analytics.symbol} />

      <h3 style={{ fontSize: '1rem', marginTop: '16px' }}>What each indicator shows right now<InfoTip term="signal" /></h3>
      <p style={{ color: COLORS.muted, fontSize: '13px', margin: '4px 0 8px' }}>
        {up} pointing up, {down} pointing down, {none} with no clear view, on daily prices up to {formatDate(analytics.lastDate)}.
        Useful detail, but acting on these short-term readings did poorly in the track record.
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

      <p style={{ color: COLORS.muted, fontSize: '12px', marginTop: '12px' }}>{TREND_DISCLAIMER}</p>
    </section>
  )
}
