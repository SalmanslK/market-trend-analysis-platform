import { COLORS, formatNumber } from '../../services/format'

interface RangeBarProps {
  low?: number
  high?: number
  /** 0 = at the low, 100 = at the high */
  position?: number
  showLabels?: boolean
}

/** Shows where the current price sits between the 52-week low and high. */
export default function RangeBar({ low, high, position, showLabels = false }: RangeBarProps) {
  if (position == null) return <span>-</span>
  const clamped = Math.min(100, Math.max(0, position))

  return (
    <div title={`52-week low ${formatNumber(low)} · high ${formatNumber(high)} · ${clamped.toFixed(0)}% of range`}>
      <div style={{ position: 'relative', height: '6px', minWidth: '80px', backgroundColor: COLORS.border, borderRadius: '3px' }}>
        <div
          style={{
            position: 'absolute',
            left: `calc(${clamped}% - 5px)`,
            top: '-3px',
            width: '10px',
            height: '12px',
            borderRadius: '2px',
            backgroundColor: '#424242',
          }}
        />
      </div>
      {showLabels && (
        <div style={{ display: 'flex', justifyContent: 'space-between', fontSize: '12px', color: COLORS.muted, marginTop: '6px' }}>
          <span>{formatNumber(low)}</span>
          <span>{formatNumber(high)}</span>
        </div>
      )}
    </div>
  )
}
