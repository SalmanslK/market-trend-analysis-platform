import { Timeframe } from '../../services/types/marketData'
import { timeframeLabels } from '../../services/labels'

interface TimeframeSelectorProps {
  selectedTimeframe: Timeframe
  onTimeframeChange: (timeframe: Timeframe) => void
}

export default function TimeframeSelector({
  selectedTimeframe,
  onTimeframeChange,
}: TimeframeSelectorProps) {
  return (
    <select
      value={selectedTimeframe}
      onChange={(e) => onTimeframeChange(Number(e.target.value) as Timeframe)}
      style={{ padding: '8px', fontSize: '16px' }}
    >
      {Object.entries(timeframeLabels).map(([value, label]) => (
        <option key={value} value={value}>
          {label}
        </option>
      ))}
    </select>
  )
}
