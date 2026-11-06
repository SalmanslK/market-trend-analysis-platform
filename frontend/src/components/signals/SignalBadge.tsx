import { TechnicalSignal } from '../../services/types/marketData'
import { signalStyles } from '../../services/format'

interface SignalBadgeProps {
  signal: TechnicalSignal
  large?: boolean
}

export default function SignalBadge({ signal, large = false }: SignalBadgeProps) {
  const style = signalStyles[signal.action]
  // Hovering shows the reasons, so the table stays compact
  const tooltip = signal.reasons
    .map((r) => `${r.impact > 0 ? '▲' : r.impact < 0 ? '▼' : '●'} ${r.indicator}: ${r.detail}`)
    .join('\n')

  return (
    <span
      title={tooltip}
      style={{
        display: 'inline-block',
        padding: large ? '6px 14px' : '3px 10px',
        borderRadius: '12px',
        fontSize: large ? '16px' : '13px',
        fontWeight: 'bold',
        whiteSpace: 'nowrap',
        backgroundColor: style.background,
        color: style.color,
        cursor: 'help',
      }}
    >
      {style.label}
      <span style={{ fontWeight: 'normal', marginLeft: '6px', opacity: 0.8 }}>
        {signal.score > 0 ? '+' : ''}{signal.score}/{signal.maxScore}
      </span>
    </span>
  )
}
