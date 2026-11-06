import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { backtestApi } from '../../services/api/backtestApi'
import { BacktestResult } from '../../services/types/marketData'
import { describeTrendRecord } from '../../services/plainLanguage'
import InfoTip from '../help/InfoTip'

interface TrendTrackRecordProps {
  symbol: string
}

/** Shows next to the trend label how acting on it would have compared with simply holding. */
export default function TrendTrackRecord({ symbol }: TrendTrackRecordProps) {
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

  return (
    <div style={{ margin: '12px 0', padding: '10px 12px', backgroundColor: '#f5f5f5', borderRadius: '6px', fontSize: '13px', lineHeight: 1.45 }}>
      <strong>If you had acted on this label:<InfoTip term="backtest" /></strong>{' '}
      {describeTrendRecord(result)}{' '}
      <Link to={`/track-record?symbol=${symbol}`} style={{ color: 'inherit', textDecoration: 'underline' }}>See the details</Link>
    </div>
  )
}
