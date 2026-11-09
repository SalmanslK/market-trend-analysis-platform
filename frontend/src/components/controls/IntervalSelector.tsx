import { Interval } from '../../services/types/marketData'
import { intervalLabels } from '../../services/labels'

interface IntervalSelectorProps {
  selectedInterval: Interval
  onIntervalChange: (interval: Interval) => void
}

export default function IntervalSelector({
  selectedInterval,
  onIntervalChange,
}: IntervalSelectorProps) {
  return (
    <select
      aria-label="Price interval: one point per day, week, month..."
      value={selectedInterval}
      onChange={(e) => onIntervalChange(Number(e.target.value) as Interval)}
      style={{ padding: '8px', fontSize: '16px' }}
    >
      {Object.entries(intervalLabels).map(([value, label]) => (
        <option key={value} value={value}>
          {label}
        </option>
      ))}
    </select>
  )
}
