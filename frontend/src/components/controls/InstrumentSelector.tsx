import { InstrumentDto } from '../../services/types/marketData'

interface InstrumentSelectorProps {
  instruments: InstrumentDto[]
  selectedInstrument: string
  onInstrumentChange: (symbol: string) => void
}

export default function InstrumentSelector({
  instruments,
  selectedInstrument,
  onInstrumentChange,
}: InstrumentSelectorProps) {
  return (
    <select
      value={selectedInstrument}
      onChange={(e) => onInstrumentChange(e.target.value)}
      style={{ padding: '8px', fontSize: '16px' }}
    >
      <option value="">Select Instrument</option>
      {instruments.map((instrument) => (
        <option key={instrument.id} value={instrument.symbol}>
          {instrument.symbol} - {instrument.name}
        </option>
      ))}
    </select>
  )
}