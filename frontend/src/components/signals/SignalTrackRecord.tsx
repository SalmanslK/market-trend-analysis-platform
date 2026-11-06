import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { backtestApi } from '../../services/api/backtestApi'
import { BacktestResult } from '../../services/types/marketData'
import { describeBacktest, verdictStyles } from '../../services/plainLanguage'
import InfoTip from '../help/InfoTip'

interface SignalTrackRecordProps {
  symbol: string
}

/** Shows next to a signal how far that signal could have been trusted in the past. */
export default function SignalTrackRecord({ symbol }: SignalTrackRecordProps) {
  const [result, setResult] = useState<BacktestResult | null>(null)

  useEffect(() => {
    let cancelled = false
    setResult(null)
    backtestApi.getBySymbol(symbol)
      .then((data) => { if (!cancelled) setResult(data) })
      .catch((err) => console.error('Error loading track record:', err))
    return () => { cancelled = true }
  }, [symbol])

  if (!result?.hasEnoughData) return null
  const style = verdictStyles[result.verdict]

  return (
    <div style={{ marginTop: '12px', padding: '10px 12px', backgroundColor: style.background, color: style.color, borderRadius: '6px', fontSize: '13px', lineHeight: 1.45 }}>
      <strong>How far to trust this signal: {style.label.toLowerCase()}.<InfoTip term="backtest" /></strong>{' '}
      {describeBacktest(result)}{' '}
      <Link to={`/track-record?symbol=${symbol}`} style={{ color: 'inherit', textDecoration: 'underline' }}>See the details</Link>
    </div>
  )
}
