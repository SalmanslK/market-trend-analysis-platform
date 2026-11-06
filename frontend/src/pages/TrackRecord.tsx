import { useEffect, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import {
  CartesianGrid,
  Legend,
  Line,
  LineChart,
  ReferenceLine,
  ResponsiveContainer,
  Tooltip,
  XAxis,
  YAxis,
} from 'recharts'
import { backtestApi } from '../services/api/backtestApi'
import { BacktestResult, SignalAction, SignalOutcome } from '../services/types/marketData'
import { changeColor, COLORS, formatDate, formatPercent, signalStyles } from '../services/format'
import { describeBacktest, describeBacktests, MIN_RELIABLE_YEARS, verdictStyles } from '../services/plainLanguage'
import InfoTip from '../components/help/InfoTip'
import PlainSummary from '../components/help/PlainSummary'

const COST_OPTIONS = [0, 0.25, 0.5, 1, 2]
const DEFAULT_COST = 0.5
const OUTCOME_HORIZON_DAYS = 30

const cellStyle = { padding: '10px', borderBottom: `1px solid ${COLORS.border}`, textAlign: 'right' as const }
const headerStyle = { ...cellStyle, backgroundColor: '#f5f5f5', whiteSpace: 'nowrap' as const }
const cardStyle = { padding: '20px', backgroundColor: 'white', borderRadius: '6px', marginTop: '12px' }

function VerdictChip({ result }: { result: BacktestResult }) {
  const style = verdictStyles[result.verdict]
  return (
    <span style={{ padding: '3px 10px', borderRadius: '12px', fontSize: '13px', fontWeight: 'bold', whiteSpace: 'nowrap', backgroundColor: style.background, color: style.color }}>
      {style.label}
    </span>
  )
}

function outcomeLabel(outcome: SignalOutcome): string {
  return outcome.action === 'All' ? 'Any day (for comparison)' : signalStyles[outcome.action as SignalAction].label
}

function InstrumentDetail({ result }: { result: BacktestResult }) {
  const curve = result.equityCurve.map((p) => ({
    date: new Date(p.date).toLocaleDateString(),
    'Simply holding': p.buyAndHold,
    'Following the signal': p.strategy,
  }))
  const horizonFor = (o: SignalOutcome) => o.horizons.find((h) => h.horizonDays === OUTCOME_HORIZON_DAYS)
  const spread = result.buyMinusSell30D
  const first = result.buyMinusSell30DFirstHalf
  const second = result.buyMinusSell30DSecondHalf

  return (
    <>
      <section style={cardStyle}>
        <h2 style={{ fontSize: '1.2rem' }}>{result.symbol}: what 100 would have become</h2>
        <p style={{ fontSize: '13px', color: '#546e7a', margin: '4px 0 10px', lineHeight: 1.45 }}>
          {describeBacktest(result)} Tested from {formatDate(result.startDate)} to {formatDate(result.endDate)}, with {result.strategy.trades} trades,
          invested {result.strategy.timeInvested.toFixed(0)}% of the time.
        </p>
        <div style={{ width: '100%', height: '320px' }}>
          <ResponsiveContainer width="100%" height="100%">
            <LineChart data={curve} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
              <CartesianGrid strokeDasharray="3 3" />
              <XAxis dataKey="date" minTickGap={50} />
              <YAxis domain={['auto', 'auto']} tickFormatter={(value: number) => value.toLocaleString()} />
              <Tooltip formatter={(value) => (typeof value === 'number' ? value.toFixed(1) : value)} />
              <Legend />
              <ReferenceLine y={100} stroke="#999" />
              <Line type="monotone" dataKey="Simply holding" stroke="#000000" strokeWidth={2} dot={false} isAnimationActive={false} />
              <Line type="monotone" dataKey="Following the signal" stroke="#1976d2" strokeWidth={2} dot={false} isAnimationActive={false} />
            </LineChart>
          </ResponsiveContainer>
        </div>
        <p style={{ fontSize: '13px', color: '#546e7a', marginTop: '8px' }}>
          If cash had earned {result.cashInterestPercent}% a year while out of the market (an assumption, as real rates vary),
          following the signal would have earned {formatPercent(result.strategyWithInterest.annualReturn, 1)} a year.
        </p>
      </section>

      <section style={cardStyle}>
        <h2 style={{ fontSize: '1.2rem' }}>What happened in the {OUTCOME_HORIZON_DAYS} days after each signal</h2>
        <p style={{ fontSize: '13px', color: '#546e7a', margin: '4px 0 10px', lineHeight: 1.45 }}>
          A useful signal would show clearly better results after Buy than after Sell.
          {spread != null && ` Here, Buy-type signals were followed by changes ${Math.abs(spread).toFixed(1)} percentage points ${spread >= 0 ? 'better' : 'worse'} than Sell-type signals.`}
          {first != null && second != null && ` (${formatPercent(first, 1)} in the first half of the period, ${formatPercent(second, 1)} in the second.)`}
        </p>
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
            <thead>
              <tr>
                <th style={{ ...headerStyle, textAlign: 'left' }}>After a...</th>
                <th style={headerStyle}>Average change</th>
                <th style={headerStyle}>Typical (median)</th>
                <th style={headerStyle}>Times it rose</th>
                <th style={headerStyle}>Days</th>
                <th style={headerStyle}>
                  Separate periods
                  <InfoTip title="Separate periods" text="Days next to each other share most of their 30-day window, so they aren't independent. The number of separate runs of a signal is a better guide to how much evidence there is." />
                </th>
              </tr>
            </thead>
            <tbody>
              {result.outcomes.map((o) => {
                const horizon = horizonFor(o)
                return (
                  <tr key={o.action} style={o.action === 'All' ? { fontWeight: 'bold', backgroundColor: '#fafafa' } : undefined}>
                    <td style={{ ...cellStyle, textAlign: 'left' }}>{outcomeLabel(o)}</td>
                    <td style={{ ...cellStyle, color: changeColor(horizon?.averageChange) }}>{formatPercent(horizon?.averageChange)}</td>
                    <td style={{ ...cellStyle, color: changeColor(horizon?.medianChange) }}>{formatPercent(horizon?.medianChange)}</td>
                    <td style={cellStyle}>{horizon ? `${horizon.percentPositive.toFixed(0)}%` : '-'}</td>
                    <td style={cellStyle}>{o.days.toLocaleString()}</td>
                    <td style={cellStyle}>{o.action === 'All' ? '-' : o.periods}</td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
      </section>
    </>
  )
}

export default function TrackRecord() {
  const [params, setParams] = useSearchParams()
  const selected = params.get('symbol') ?? ''
  const [cost, setCost] = useState(DEFAULT_COST)
  const [results, setResults] = useState<BacktestResult[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    backtestApi.getAll(cost)
      .then((data) => { if (!cancelled) setResults(data) })
      .catch((err) => {
        console.error('Error loading track record:', err)
        if (!cancelled) setError('Could not load the track record. Check that the API is running.')
      })
      .finally(() => { if (!cancelled) setLoading(false) })
    return () => { cancelled = true }
  }, [cost])

  const tested = results.filter((r) => r.hasEnoughData)
  const notTested = results.filter((r) => !r.hasEnoughData)
  const detail = tested.find((r) => r.symbol === selected) ?? tested.find((r) => r.symbol === 'KSE100') ?? tested[0]

  return (
    <div style={{ padding: '20px' }}>
      <h1>Track Record</h1>
      <p style={{ color: COLORS.muted, margin: '6px 0 16px' }}>
        Would following the Buy and Sell signals have worked? This replays them over past prices.<InfoTip term="backtest" />
      </p>

      {tested.length > 0 && <PlainSummary points={describeBacktests(tested, cost)} />}

      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
        <label htmlFor="cost">Cost per trade<InfoTip term="tradingCost" /></label>
        <select id="cost" value={cost} onChange={(e) => setCost(Number(e.target.value))} style={{ padding: '8px', fontSize: '15px' }}>
          {COST_OPTIONS.map((option) => <option key={option} value={option}>{option}%</option>)}
        </select>
        {loading && <span style={{ color: COLORS.muted }}>Calculating...</span>}
      </div>

      {error && <div style={{ color: COLORS.down, marginBottom: '12px' }}>{error}</div>}

      <div style={{ overflowX: 'auto', backgroundColor: 'white', borderRadius: '6px' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
          <thead>
            <tr>
              <th style={{ ...headerStyle, textAlign: 'left' }}>Instrument</th>
              <th style={headerStyle}>Years tested</th>
              <th style={headerStyle}>Holding, per year<InfoTip term="annualReturn" /></th>
              <th style={headerStyle}>Signal, per year</th>
              <th style={headerStyle}>Worst fall, holding<InfoTip term="drawdown" /></th>
              <th style={headerStyle}>Worst fall, signal</th>
              <th style={headerStyle}>Trades</th>
              <th style={{ ...headerStyle, textAlign: 'center' }}>Result</th>
            </tr>
          </thead>
          <tbody>
            {tested.map((r) => (
              <tr
                key={r.symbol}
                onClick={() => setParams({ symbol: r.symbol }, { replace: true })}
                style={{ cursor: 'pointer', backgroundColor: detail?.symbol === r.symbol ? '#e8f1fb' : undefined }}
              >
                <td style={{ ...cellStyle, textAlign: 'left' }}>
                  <strong style={{ color: COLORS.link }}>{r.symbol}</strong>
                  <div style={{ fontSize: '12px', color: COLORS.muted }}>{r.name}</div>
                </td>
                <td style={cellStyle}>
                  {r.years.toFixed(1)}
                  {r.years < MIN_RELIABLE_YEARS && <div style={{ fontSize: '12px', color: COLORS.down }}>too short to judge</div>}
                </td>
                <td style={{ ...cellStyle, color: changeColor(r.buyAndHold.annualReturn) }}>{formatPercent(r.buyAndHold.annualReturn, 1)}</td>
                <td style={{ ...cellStyle, color: changeColor(r.strategy.annualReturn) }}>{formatPercent(r.strategy.annualReturn, 1)}</td>
                <td style={cellStyle}>{r.buyAndHold.maxDrawdown.toFixed(0)}%</td>
                <td style={cellStyle}>{r.strategy.maxDrawdown.toFixed(0)}%</td>
                <td style={cellStyle}>{r.strategy.trades}</td>
                <td style={{ ...cellStyle, textAlign: 'center' }}><VerdictChip result={r} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      {notTested.length > 0 && (
        <p style={{ color: COLORS.muted, fontSize: '13px', marginTop: '8px' }}>Not enough history to test: {notTested.map((r) => r.symbol).join(', ')}.</p>
      )}
      <p style={{ color: COLORS.muted, fontSize: '13px', marginTop: '8px' }}>Click an instrument for its details.</p>

      {detail && <InstrumentDetail result={detail} />}

      <section style={{ ...cardStyle, backgroundColor: '#fff8e1', lineHeight: 1.5 }}>
        <h2 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>How this test works, and its limits</h2>
        <ul style={{ paddingLeft: '20px' }}>
          <li><strong>The rule tested:</strong> invest after a Buy signal, move to cash after a Sell signal, stay put on Hold. "Holding" means buying once and doing nothing.</li>
          <li><strong>Fair test:</strong> each day uses only the prices known that day, trades happen at the next day's close, and every trade pays the cost above. The signal's rules use standard settings and were not adjusted to fit this data.</li>
          <li><strong>Dividends aren't included.</strong> Index and fund prices leave out dividends, so real returns from holding were higher, especially for funds like MIF, whose price drops at each payout.</li>
          <li><strong>Cash earns nothing</strong> in the main result, which is harsh on the signal when interest rates are high. The figure with interest under each chart shows the difference.</li>
          <li><strong>The history is mostly a rising market,</strong> which favours holding. A long falling market could look different, and past results don't guarantee future ones.</li>
          <li>The news mood isn't part of this test: news has only been collected recently.</li>
        </ul>
        <p style={{ marginTop: '8px' }}>This is not financial advice. See the <Link to="/guide" style={{ color: COLORS.link }}>Guide</Link> for what each term means.</p>
      </section>
    </div>
  )
}
