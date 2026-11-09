import { useEffect, useState, useMemo } from 'react'
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
import { BacktestResult, RuleResult, SignalAction, SignalOutcome } from '../services/types/marketData'
import { changeColor, COLORS, formatDate, formatPercent, signalStyles, SURFACE } from '../services/format'
import { compareWithHolding, describeBacktests, describeRuleRecord, describeTrendRules, MIN_RELIABLE_YEARS, ruleNames, verdictStyles } from '../services/plainLanguage'
import InfoTip from '../components/help/InfoTip'
import PlainSummary from '../components/help/PlainSummary'
import { applyBacktestMode, useSignalMode } from '../services/signalMode'
import { useFeatures } from '../services/features'
import AnswerCard from '../components/help/AnswerCard'
import { trackRecordAnswer } from '../services/answers'
import NewsSignalComparison from '../components/signals/NewsSignalComparison'
import LiveRecord from '../components/signals/LiveRecord'

const COST_OPTIONS = [0, 0.25, 0.5, 1, 2]
const DEFAULT_COST = 0.5
const OUTCOME_HORIZON_DAYS = 30

const cellStyle = { padding: '10px', borderBottom: `1px solid ${COLORS.border}`, textAlign: 'right' as const }
const headerStyle = { ...cellStyle, backgroundColor: '#f5f5f5', whiteSpace: 'nowrap' as const }
const cardStyle = { padding: '20px', ...SURFACE, marginTop: '12px' }
const noteStyle = { fontSize: '13px', color: '#546e7a', lineHeight: 1.45 }

const RULE_COLORS = { ahead: COLORS.up, tied: COLORS.neutral, behind: COLORS.down }

function VerdictChip({ result }: { result: BacktestResult }) {
  const style = verdictStyles[result.verdict]
  return (
    <span style={{ padding: '3px 10px', borderRadius: '12px', fontSize: '13px', fontWeight: 'bold', whiteSpace: 'nowrap', backgroundColor: style.background, color: style.color }}>
      {style.label}
    </span>
  )
}

function RuleCell({ result, ruleKey }: { result: BacktestResult; ruleKey: RuleResult['key'] }) {
  const rule = result.rules.find((r) => r.key === ruleKey)
  if (!rule) return <td style={cellStyle}>-</td>
  const comparison = compareWithHolding(rule.performance.annualReturn, result.buyAndHold.annualReturn)
  return (
    <td style={cellStyle}>
      <strong style={{ color: RULE_COLORS[comparison] }}>{formatPercent(rule.performance.annualReturn, 1)}</strong>
      <div style={{ fontSize: '12px', color: COLORS.muted }}>
        {rule.performance.trades} trades, fall {rule.performance.maxDrawdown.toFixed(0)}%
      </div>
    </td>
  )
}

function InstrumentDetail({ result }: { result: BacktestResult }) {
  const curve = result.equityCurve.map((p) => ({
    date: new Date(p.date).toLocaleDateString(),
    'Simply holding': p.buyAndHold,
    [ruleNames.goldenCross]: p.goldenCross,
    [ruleNames.monthlyTrend]: p.monthlyTrend,
    [ruleNames.signal]: p.strategy,
  }))

  return (
    <section style={cardStyle}>
      <h2 style={{ fontSize: '1.2rem' }}>{result.symbol}: what 100 would have become</h2>
      <p style={{ ...noteStyle, margin: '4px 0 10px' }}>
        Following the long-term trend: {describeRuleRecord(result, 'goldenCross')} Tested from {formatDate(result.startDate)} to {formatDate(result.endDate)}.
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
            <Line type="monotone" dataKey={ruleNames.goldenCross} stroke="#2e7d32" strokeWidth={2} dot={false} isAnimationActive={false} />
            <Line type="monotone" dataKey={ruleNames.monthlyTrend} stroke="#ef6c00" strokeWidth={1.5} dot={false} isAnimationActive={false} />
            <Line type="monotone" dataKey={ruleNames.signal} stroke="#1976d2" strokeWidth={1.5} dot={false} isAnimationActive={false} />
          </LineChart>
        </ResponsiveContainer>
      </div>
    </section>
  )
}

function outcomeLabel(outcome: SignalOutcome): string {
  return outcome.action === 'All' ? 'Any day (for comparison)' : signalStyles[outcome.action as SignalAction].label
}

function OldScoreSection({ results, detail, cost }: { results: BacktestResult[]; detail?: BacktestResult; cost: number }) {
  const horizonFor = (o: SignalOutcome) => o.horizons.find((h) => h.horizonDays === OUTCOME_HORIZON_DAYS)
  const spread = detail?.buyMinusSell30D
  const first = detail?.buyMinusSell30DFirstHalf
  const second = detail?.buyMinusSell30DSecondHalf

  return (
    <section style={cardStyle}>
      <h2 style={{ fontSize: '1.2rem' }}>How the technical Buy/Sell signal did</h2>
      <p style={{ ...noteStyle, margin: '4px 0 10px' }}>
        The technical signal adds up five indicators into Strong Buy, Buy, Hold, Sell or Strong Sell (steady or daily, as chosen at the top). This is what acting on it would have done.
      </p>

      <PlainSummary points={describeBacktests(results, cost)} />

      {/* The same results per instrument, and what followed each signal: detail most readers can skip */}
      <details>
        <summary style={{ cursor: 'pointer', color: COLORS.link, fontSize: '14px', padding: '4px 0' }}>
          Show each instrument's result and what happened after each signal
        </summary>
      <div style={{ overflowX: 'auto', marginTop: '8px' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
          <thead>
            <tr>
              <th style={{ ...headerStyle, textAlign: 'left' }}>Instrument</th>
              <th style={headerStyle}>Holding, per year</th>
              <th style={headerStyle}>Signal, per year</th>
              <th style={headerStyle}>Trades</th>
              <th style={{ ...headerStyle, textAlign: 'center' }}>Result</th>
            </tr>
          </thead>
          <tbody>
            {results.map((r) => (
              <tr key={r.symbol}>
                <td style={{ ...cellStyle, textAlign: 'left' }}>
                  <strong>{r.symbol}</strong>
                  {r.years < MIN_RELIABLE_YEARS && <div style={{ fontSize: '12px', color: COLORS.down }}>too short to judge</div>}
                </td>
                <td style={{ ...cellStyle, color: changeColor(r.buyAndHold.annualReturn) }}>{formatPercent(r.buyAndHold.annualReturn, 1)}</td>
                <td style={{ ...cellStyle, color: changeColor(r.strategy.annualReturn) }}>{formatPercent(r.strategy.annualReturn, 1)}</td>
                <td style={cellStyle}>{r.strategy.trades}</td>
                <td style={{ ...cellStyle, textAlign: 'center' }}><VerdictChip result={r} /></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {detail && (
        <>
          <h3 style={{ fontSize: '1rem', marginTop: '20px' }}>{detail.symbol}: what happened in the {OUTCOME_HORIZON_DAYS} days after each signal</h3>
          <p style={{ ...noteStyle, margin: '4px 0 10px' }}>
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
                {detail.outcomes.map((o) => {
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
        </>
      )}
      </details>
    </section>
  )
}

// Results of scripts/research/risk-rules-research.cs: the rule and the pass mark were written down before the test
const CASH_RULE_RESULTS = [
  { period: '2006-2015, to check the idea', holding: 14.4, holdingFall: -63, rule: 12.8, ruleFall: -25 },
  { period: '2016-2026, held back', holding: 12.7, holdingFall: -57, rule: 15.6, ruleFall: -23 },
]

/** The one rule that passed a test fixed in advance, with its numbers and limits. */
function StrongerThanCashResult() {
  return (
    <section aria-labelledby="cash-rule" style={{ ...cardStyle, borderTop: `4px solid ${COLORS.up}` }}>
      <h2 id="cash-rule" style={{ fontSize: '1.2rem', marginBottom: '6px' }}>The rule that passed: stronger than cash<InfoTip term="strongerThanCash" /></h2>
      <p style={{ fontSize: '14px', color: '#334e68', lineHeight: 1.5 }}>
        Once a month, stay in stocks only if they earned more than cash over the past 12 months; otherwise wait in cash.
        We wrote the rule and what would count as a pass down before testing it, checked it on 2006 to 2015, then looked once at
        2016 to 2026, which had been held back. It passed both: about the same return as holding, with a much smaller worst fall.
      </p>
      <div style={{ overflowX: 'auto', marginTop: '10px' }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
          <thead>
            <tr>
              <th style={{ ...headerStyle, textAlign: 'left', whiteSpace: 'normal' }}>Meezan Islamic Fund</th>
              <th style={headerStyle}>Holding</th>
              <th style={{ ...headerStyle, whiteSpace: 'normal' }}>Stronger than cash</th>
            </tr>
          </thead>
          <tbody>
            {CASH_RULE_RESULTS.map((r) => (
              <tr key={r.period}>
                <td style={{ ...cellStyle, textAlign: 'left' }}>{r.period}</td>
                <td style={cellStyle}>{formatPercent(r.holding, 1)}<div style={{ fontSize: '12px', color: COLORS.muted }}>fall {r.holdingFall}%</div></td>
                <td style={cellStyle}><strong>{formatPercent(r.rule, 1)}</strong><div style={{ fontSize: '12px', color: COLORS.up, fontWeight: 600 }}>fall {r.ruleFall}%</div></td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <p style={{ ...noteStyle, marginTop: '8px' }}>
        Figures are returns a year with payouts included, and the worst fall from a peak. It switched about once a year, at 0.5% a trade, with cash earning the State Bank rate less 1%. Limits: about four big falls in
        21 years decide the result, Pakistan's cash rates were unusually high (up to 21%), and it was tested on stocks, not gold.
        Each instrument's page shows where it stands now. Volatility-based and news-based rules were tested the same way and failed.
      </p>
    </section>
  )
}

export default function TrackRecord() {
  const [params, setParams] = useSearchParams()
  const selected = params.get('symbol') ?? ''
  const [cost, setCost] = useState(DEFAULT_COST)
  const [loaded, setResults] = useState<BacktestResult[]>([])
  // The technical and combined signals chosen at the top of the page
  const mode = useSignalMode()
  // The news and combined comparison only when news is switched on (Features:News)
  const { news } = useFeatures()
  const results = useMemo(() => loaded.map((r) => applyBacktestMode(r, mode)), [loaded, mode])
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
  // The cost the figures on screen were calculated with; the dropdown changes before new figures arrive
  const shownCost = tested[0]?.costPercent ?? cost
  const detail = tested.find((r) => r.symbol === selected) ?? tested.find((r) => r.symbol === 'KSE100') ?? tested[0]

  return (
    <div style={{ padding: '20px' }}>
      <h1>Track Record</h1>
      <p style={{ color: COLORS.muted, margin: '6px 0 16px' }}>
        Would acting on the app's signals have worked? This replays them over past prices.<InfoTip term="backtest" />
      </p>

      {(() => {
        const verdict = trackRecordAnswer(tested)
        return verdict && (
          <AnswerCard question="Would following the signals have paid off?" answer={verdict.answer} facts={verdict.facts} meaning={verdict.meaning} />
        )
      })()}

      <StrongerThanCashResult />
      <LiveRecord />

      <h2 style={{ fontSize: '1.2rem', margin: '20px 0 6px' }}>The detail, instrument by instrument</h2>
      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '8px', marginBottom: '12px' }}>
        <label htmlFor="cost">Cost per trade<InfoTip term="tradingCost" /></label>
        <select id="cost" value={cost} onChange={(e) => setCost(Number(e.target.value))} style={{ padding: '8px', fontSize: '15px' }}>
          {COST_OPTIONS.map((option) => <option key={option} value={option}>{option}%</option>)}
        </select>
        {loading && <span style={{ color: COLORS.muted }}>Calculating...</span>}
      </div>

      {error && <div style={{ color: COLORS.down, marginBottom: '12px' }}>{error}</div>}

      {news && tested.length > 0 && (
        <NewsSignalComparison
          results={tested}
          detail={detail}
          cost={shownCost}
          onSelect={(symbol) => setParams({ symbol }, { replace: true })}
        />
      )}

      {/* While the figures are being calculated, say so instead of showing empty tables */}
      {loading && tested.length === 0 && (
        <p role="status" style={{ ...SURFACE, padding: '20px', color: COLORS.muted }}>Calculating the track record from all price history; this takes a few seconds...</p>
      )}

      {tested.length > 0 && (<>
      <h2 style={{ fontSize: '1.2rem', margin: '20px 0 8px' }}>Technical signal and trend rules, over all price history</h2>
      {tested.length > 0 && <PlainSummary points={describeTrendRules(tested, shownCost)} />}

      <div style={{ overflowX: 'auto', ...SURFACE }}>
        <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
          <thead>
            <tr>
              <th style={{ ...headerStyle, textAlign: 'left' }}>Return per year<InfoTip term="annualReturn" /></th>
              <th style={headerStyle}>Years tested</th>
              <th style={headerStyle}>Holding<InfoTip term="drawdown" /></th>
              <th style={headerStyle}>{ruleNames.goldenCross}<InfoTip term="goldenCross" /></th>
              <th style={headerStyle}>{ruleNames.monthlyTrend}<InfoTip term="monthlyTrend" /></th>
              <th style={headerStyle}>{ruleNames.signal}<InfoTip term="signal" /></th>
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
                <td style={cellStyle}>
                  <strong>{formatPercent(r.buyAndHold.annualReturn, 1)}</strong>
                  <div style={{ fontSize: '12px', color: COLORS.muted }}>fall {r.buyAndHold.maxDrawdown.toFixed(0)}%</div>
                </td>
                <RuleCell result={r} ruleKey="goldenCross" />
                <RuleCell result={r} ruleKey="monthlyTrend" />
                <RuleCell result={r} ruleKey="signal" />
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      </>)}
      {notTested.length > 0 && (
        <p style={{ color: COLORS.muted, fontSize: '13px', marginTop: '8px' }}>Not enough history to test: {notTested.map((r) => r.symbol).join(', ')}.</p>
      )}
      <p style={{ ...noteStyle, marginTop: '8px' }}>
        Green is more than 1 point a year ahead of holding, red more than 1 point behind, grey about the same. "Fall" is the worst drop from a peak.
        Click an instrument for its chart.
      </p>

      {detail && <InstrumentDetail result={detail} />}

      {tested.length > 0 && <OldScoreSection results={tested} detail={detail} cost={shownCost} />}

      <section style={{ ...cardStyle, backgroundColor: '#fff8e1', lineHeight: 1.5 }}>
        <h2 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>How this test works, and its limits</h2>
        <ul style={{ paddingLeft: '20px' }}>
          <li><strong>What is compared:</strong> being invested only while a rule says so, otherwise in cash, against buying once and doing nothing ("holding").</li>
          <li><strong>Fair test:</strong> each day uses only the prices known that day, trades happen at the next day's close, and every trade pays the cost above. All rules use standard settings and were not adjusted to fit this data.</li>
          <li><strong>The long-term trend rule was picked after seeing this test,</strong> so its figures may flatter it. The real test is how it does from here on.</li>
          <li><strong>Few trades means weak evidence.</strong> The slower rules made only a handful of trades, so one or two well-timed moves can decide the result.</li>
          <li><strong>Dividends are only partly included.</strong> Fund prices count payouts as reinvested, but index prices leave out the dividends of the stocks in them, so real returns from holding an index were somewhat higher. MIF's payouts from 2005 to 2013 aren't published, so they are estimated from its price drops on payout days.</li>
          <li><strong>Cash earns nothing here,</strong> which is harsh on every rule when interest rates are high.</li>
          <li><strong>The history is mostly a rising market,</strong> which favours holding. A long falling market could look different, and past results don't guarantee future ones.</li>
        </ul>
        <p style={{ marginTop: '8px' }}>This is not financial advice. See the <Link to="/guide" style={{ color: COLORS.link }}>Guide</Link> for what each term means.</p>
      </section>
    </div>
  )
}
