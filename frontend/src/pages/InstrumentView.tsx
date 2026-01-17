import { useEffect, useState } from 'react'
import { useParams, useNavigate } from 'react-router-dom'
import { instrumentsApi } from '../services/api/instrumentsApi'
import { pricesApi } from '../services/api/pricesApi'
import { InstrumentDto, PriceDataDto, Interval, Timeframe } from '../services/types/marketData'
import PriceChart from '../components/charts/PriceChart'
import InstrumentSelector from '../components/controls/InstrumentSelector'
import IntervalSelector from '../components/controls/IntervalSelector'
import TimeframeSelector from '../components/controls/TimeframeSelector'

export default function InstrumentView() {
  const { symbol } = useParams<{ symbol: string }>()
  const navigate = useNavigate()
  
  const [instruments, setInstruments] = useState<InstrumentDto[]>([])
  const [selectedInstrument, setSelectedInstrument] = useState<string>(symbol || '')
  const [priceData, setPriceData] = useState<PriceDataDto[]>([])
  const [loading, setLoading] = useState(true)
  const [interval, setInterval] = useState<Interval>(Interval.Daily)
  const [timeframe, setTimeframe] = useState<Timeframe>(Timeframe.Quarter)

  useEffect(() => {
    loadInstruments()
  }, [])

  useEffect(() => {
    if (selectedInstrument) {
      loadPriceData()
    }
  }, [selectedInstrument, interval, timeframe])

  const loadInstruments = async () => {
    try {
      const data = await instrumentsApi.getAll()
      setInstruments(data)
      if (symbol && !selectedInstrument) {
        setSelectedInstrument(symbol)
      }
    } catch (error) {
      console.error('Error loading instruments:', error)
    }
  }

  const loadPriceData = async () => {
    if (!selectedInstrument) return

    try {
      setLoading(true)
      const data = await pricesApi.getPriceData(selectedInstrument, interval, timeframe)
      setPriceData(data)
    } catch (error) {
      console.error('Error loading price data:', error)
    } finally {
      setLoading(false)
    }
  }

  const handleInstrumentChange = (newSymbol: string) => {
    setSelectedInstrument(newSymbol)
    navigate(`/instrument/${newSymbol}`)
  }

  if (loading && !priceData.length) {
    return <div style={{ padding: '20px' }}>Loading...</div>
  }

  const intervalLabels: Record<Interval, string> = {
    [Interval.Daily]: 'Daily',
    [Interval.Weekly]: 'Weekly',
    [Interval.Monthly]: 'Monthly',
    [Interval.Quarterly]: 'Quarterly',
    [Interval.Yearly]: 'Yearly',
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

  return (
    <div style={{ padding: '20px' }}>
      <h1>Instrument View</h1>
      
      <div style={{ marginBottom: '20px', display: 'flex', gap: '10px', alignItems: 'center' }}>
        <InstrumentSelector
          instruments={instruments}
          selectedInstrument={selectedInstrument}
          onInstrumentChange={handleInstrumentChange}
        />
        
        <IntervalSelector
          selectedInterval={interval}
          onIntervalChange={setInterval}
        />
        
        <TimeframeSelector
          selectedTimeframe={timeframe}
          onTimeframeChange={setTimeframe}
        />
      </div>

      {priceData.length > 0 ? (
        <PriceChart
          data={priceData}
          instrument={selectedInstrument}
          interval={intervalLabels[interval]}
          timeframe={timeframeLabels[timeframe]}
        />
      ) : (
        <div>No data available for this instrument.</div>
      )}
    </div>
  )
}