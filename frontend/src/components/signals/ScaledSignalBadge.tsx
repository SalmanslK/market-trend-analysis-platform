import { ScaledSignal } from '../../services/types/marketData'
import { signalStyles } from '../../services/format'

interface ScaledSignalBadgeProps {
  signal: ScaledSignal
  /** Explains the reading on hover */
  title?: string
  large?: boolean
}

/** A Buy/Sell label for a signal scored from -100 to +100. */
export default function ScaledSignalBadge({ signal, title, large = false }: ScaledSignalBadgeProps) {
  const style = signalStyles[signal.action]

  return (
    <span
      title={title}
      style={{
        display: 'inline-block',
        padding: large ? '6px 14px' : '3px 10px',
        borderRadius: '12px',
        fontSize: large ? '16px' : '13px',
        fontWeight: 'bold',
        whiteSpace: 'nowrap',
        backgroundColor: style.background,
        color: style.color,
        cursor: title ? 'help' : undefined,
      }}
    >
      {style.label}
      <span style={{ fontWeight: 'normal', marginLeft: '6px', opacity: 0.8 }}>
        {signal.score > 0 ? '+' : ''}{signal.score}
      </span>
    </span>
  )
}
