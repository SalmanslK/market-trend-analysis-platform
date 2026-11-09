import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { backtestApi } from '../../services/api/backtestApi'
import { BacktestResult } from '../../services/types/marketData'
import { describeNewsRuleRecord, describeRuleRecord } from '../../services/plainLanguage'
import { useFeatures } from '../../services/features'
import InfoTip from '../help/InfoTip'

interface SignalTrackRecordProps {
  symbol: string
}

/** Shows next to the signal and trend how acting on them would have compared with simply holding. */
export default function SignalTrackRecord({ symbol }: SignalTrackRecordProps) {
  const { news } = useFeatures()
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
      <strong>In the past, following the technical signal:<InfoTip term="backtest" /></strong> {describeRuleRecord(result, 'signal')}
      {news && result.newsTest.hasEnoughNews && (
        <>
          <div style={{ marginTop: '6px' }}><strong>The news signal:</strong> {describeNewsRuleRecord(result, 'news')}</div>
          <div style={{ marginTop: '6px' }}><strong>The combined signal:</strong> {describeNewsRuleRecord(result, 'combined')}</div>
        </>
      )}
      <div style={{ marginTop: '6px' }}>
        <strong>Following the long-term trend:</strong> {describeRuleRecord(result, 'goldenCross')}{' '}
        <Link to={`/track-record?symbol=${symbol}`} style={{ color: 'inherit', textDecoration: 'underline' }}>See the details</Link>
      </div>
    </div>
  )
}
