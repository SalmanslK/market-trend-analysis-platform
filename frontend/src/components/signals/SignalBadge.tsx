import { TechnicalSignal } from '../../services/types/marketData'
import { signalStyles } from '../../services/format'
import Tip from '../help/Tip'

interface SignalBadgeProps {
  signal: TechnicalSignal
  large?: boolean
}

export default function SignalBadge({ signal, large = false }: SignalBadgeProps) {
  const style = signalStyles[signal.action]
  // On the same -100 to +100 scale as the news and combined signals
  const scaled = signal.maxScore > 0 ? Math.round((signal.score / signal.maxScore) * 100) : 0
  // Hovering or tapping shows the reasons, so the table stays compact
  const tooltip = signal.reasons
    .map((r) => `${r.impact > 0 ? '▲' : r.impact < 0 ? '▼' : '●'} ${r.indicator}: ${r.detail}`)
    .join('\n')

  return (
    <Tip text={tooltip}>
    <span
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
        {scaled > 0 ? '+' : ''}{scaled}
      </span>
    </span>
    </Tip>
  )
}
