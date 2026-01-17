import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { instrumentsApi } from '../services/api/instrumentsApi'
import { summaryApi } from '../services/api/summaryApi'
import { InstrumentDto, SummaryDto, Interval, Timeframe } from '../services/types/marketData'

export default function Dashboard() {
  const [instruments, setInstruments] = useState<InstrumentDto[]>([])
  const [summary, setSummary] = useState<SummaryDto[]>([])
  const [loading, setLoading] = useState(true)
  const [interval, setInterval] = useState<Interval>(Interval.Daily)
  const [timeframe, setTimeframe] = useState<Timeframe>(Timeframe.Quarter)

  useEffect(() => {
    loadData()
  }, [interval, timeframe])

  const loadData = async () => {
    try {
      setLoading(true)
      const [instrumentsData, summaryData] = await Promise.all([
        instrumentsApi.getAll(),
        summaryApi.getSummary(interval, timeframe),
      ])
      setInstruments(instrumentsData)
      setSummary(summaryData)
    } catch (error) {
      console.error('Error loading data:', error)
    } finally {
      setLoading(false)
    }
  }

  if (loading) {
    return <div style={{ padding: '20px' }}>Loading...</div>
  }

  return (
    <div style={{ padding: '20px' }}>
      <h1>Market Data Analytics Dashboard</h1>
      
      <div style={{ marginBottom: '20px', display: 'flex', gap: '10px' }}>
        <label>
          Interval:
          <select value={interval} onChange={(e) => setInterval(Number(e.target.value) as Interval)}>
            <option value={Interval.Daily}>Daily</option>
            <option value={Interval.Weekly}>Weekly</option>
            <option value={Interval.Monthly}>Monthly</option>
            <option value={Interval.Quarterly}>Quarterly</option>
            <option value={Interval.Yearly}>Yearly</option>
          </select>
        </label>
        
        <label>
          Timeframe:
          <select value={timeframe} onChange={(e) => setTimeframe(Number(e.target.value) as Timeframe)}>
            <option value={Timeframe.Day}>Day</option>
            <option value={Timeframe.Week}>Week</option>
            <option value={Timeframe.Month}>Month</option>
            <option value={Timeframe.Quarter}>Quarter</option>
            <option value={Timeframe.HalfYear}>Half-Year</option>
            <option value={Timeframe.Year}>Year</option>
            <option value={Timeframe.Ytd}>YTD</option>
            <option value={Timeframe.All}>All</option>
          </select>
        </label>
      </div>

      <h2>Summary Table</h2>
      <table style={{ width: '100%', borderCollapse: 'collapse', marginTop: '10px' }}>
        <thead>
          <tr style={{ backgroundColor: '#f0f0f0' }}>
            <th style={{ padding: '10px', border: '1px solid #ddd' }}>Instrument</th>
            <th style={{ padding: '10px', border: '1px solid #ddd' }}>Date</th>
            <th style={{ padding: '10px', border: '1px solid #ddd' }}>Price</th>
            <th style={{ padding: '10px', border: '1px solid #ddd' }}>SMA-20</th>
            <th style={{ padding: '10px', border: '1px solid #ddd' }}>Upper Band</th>
            <th style={{ padding: '10px', border: '1px solid #ddd' }}>Lower Band</th>
          </tr>
        </thead>
        <tbody>
          {summary.map((row) => (
            <tr key={row.instrument}>
              <td style={{ padding: '10px', border: '1px solid #ddd' }}>
                <Link 
                  to={`/instrument/${row.instrument}`}
                  style={{ color: '#1976d2', textDecoration: 'none' }}
                >
                  {row.instrument}
                </Link>
              </td>
              <td style={{ padding: '10px', border: '1px solid #ddd' }}>
                {new Date(row.date).toLocaleDateString()}
              </td>
              <td style={{ padding: '10px', border: '1px solid #ddd' }}>{row.price.toFixed(2)}</td>
              <td style={{ padding: '10px', border: '1px solid #ddd' }}>
                {row.sma20?.toFixed(2) ?? '-'}
              </td>
              <td style={{ padding: '10px', border: '1px solid #ddd' }}>
                {row.upperBand?.toFixed(2) ?? '-'}
              </td>
              <td style={{ padding: '10px', border: '1px solid #ddd' }}>
                {row.lowerBand?.toFixed(2) ?? '-'}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  )
}