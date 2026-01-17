import { Interval } from '../../services/types/marketData'

interface IntervalSelectorProps {
  selectedInterval: Interval
  onIntervalChange: (interval: Interval) => void
}

const intervalLabels: Record<Interval, string> = {
  [Interval.Daily]: 'Daily',
  [Interval.Weekly]: 'Weekly',
  [Interval.Monthly]: 'Monthly',
  [Interval.Quarterly]: 'Quarterly',
  [Interval.Yearly]: 'Yearly',
}

export default function IntervalSelector({
  selectedInterval,
  onIntervalChange,
}: IntervalSelectorProps) {
  return (
    <select
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