import { Timeframe } from '../../services/types/marketData'

interface TimeframeSelectorProps {
  selectedTimeframe: Timeframe
  onTimeframeChange: (timeframe: Timeframe) => void
}

const timeframeLabels: Record<Timeframe, string> = {
  [Timeframe.Day]: 'Day',
  [Timeframe.Week]: 'Week',
  [Timeframe.Month]: 'Month',
  [Timeframe.Quarter]: 'Quarter',
  [Timeframe.HalfYear]: 'Half-Year',
  [Timeframe.Year]: 'Year',
  [Timeframe.Ytd]: 'YTD',
  [Timeframe.All]: 'All',
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