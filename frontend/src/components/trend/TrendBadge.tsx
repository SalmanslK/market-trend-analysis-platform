import { LongTermTrend } from '../../services/types/marketData'
import { formatDate } from '../../services/format'

interface TrendBadgeProps {
  trend: LongTermTrend
  large?: boolean
}

// Deliberately soft colors: this describes a trend, it isn't an instruction to act
const styles = {
  Up: { icon: '▲', label: 'Up', background: '#e6f4ea', color: '#1b5e20' },
  Down: { icon: '▼', label: 'Down', background: '#fdeee9', color: '#a23a12' },
  Unknown: { icon: '●', label: 'Not enough data', background: '#eeeeee', color: '#616161' },
}

/** The headline label for an instrument: the direction of its long-term trend. */
export default function TrendBadge({ trend, large = false }: TrendBadgeProps) {
  const style = styles[trend.direction]
  const since = trend.since ? `since ${formatDate(trend.since)}` : undefined

  return (
    <span
      title={trend.direction === 'Unknown'
        ? 'The long-term trend needs at least 200 prices'
        : `Long-term trend: ${style.label}${since ? `, ${since}` : ''}. This describes the past and isn't a recommendation.`}
      style={{
        display: 'inline-block',
        padding: large ? '6px 14px' : '3px 10px',
        borderRadius: '12px',
        fontSize: large ? '16px' : '13px',
        fontWeight: 'bold',
        whiteSpace: 'nowrap',
        backgroundColor: style.background,
        color: style.color,
      }}
    >
      {style.icon} {style.label}
      {large && since && <span style={{ fontWeight: 'normal', marginLeft: '6px', fontSize: '13px' }}>{since}</span>}
    </span>
  )
}
