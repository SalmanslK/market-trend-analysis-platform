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
import { BacktestResult, NewsRuleResult } from '../../services/types/marketData'
import { COLORS, formatDate, formatPercent } from '../../services/format'
import { compareWithHolding, describeNewsTests, MIN_RELIABLE_YEARS } from '../../services/plainLanguage'
import InfoTip from '../help/InfoTip'
import PlainSummary from '../help/PlainSummary'

interface NewsSignalComparisonProps {
  results: BacktestResult[]
  detail?: BacktestResult
  cost: number
  onSelect: (symbol: string) => void
}

const cellStyle = { padding: '10px', borderBottom: `1px solid ${COLORS.border}`, textAlign: 'right' as const }
const headerStyle = { ...cellStyle, backgroundColor: '#f5f5f5', whiteSpace: 'nowrap' as const }
const RULE_COLORS = { ahead: COLORS.up, tied: COLORS.neutral, behind: COLORS.down }

function RuleCell({ result, ruleKey }: { result: BacktestResult; ruleKey: NewsRuleResult['key'] }) {
  const rule = result.newsTest.rules.find((r) => r.key === ruleKey)
  if (!rule) return <td style={cellStyle}>-</td>
  const comparison = compareWithHolding(rule.performance.annualReturn, result.newsTest.holding.annualReturn)
  return (
    <td style={cellStyle}>
      <strong style={{ color: RULE_COLORS[comparison] }}>{formatPercent(rule.performance.annualReturn, 1)}</strong>
      <div style={{ fontSize: '12px', color: COLORS.muted }}>
        {rule.performance.trades} trades, fall {rule.performance.maxDrawdown.toFixed(0)}%
      </div>
    </td>
  )
}

/** Technical, news and combined signals against holding, on the same dates for each instrument. */
export default function NewsSignalComparison({ results, detail, cost, onSelect }: NewsSignalComparisonProps) {
  const tested = results.filter((r) => r.newsTest.hasEnoughNews)
  const shown = detail?.newsTest.hasEnoughNews ? detail : tested.find((r) => r.symbol === 'KSE100') ?? tested[0]
  const untested = results.filter((r) => !r.newsTest.hasEnoughNews).map((r) => r.symbol)

  const curve = shown?.newsTest.equityCurve.map((p) => ({
    date: new Date(p.date).toLocaleDateString(),
    'Simply holding': p.holding,
    Technical: p.technical,
    News: p.news,
    Combined: p.combined,
  }))

  return (
    <section style={{ padding: '20px', backgroundColor: 'white', borderRadius: '6px', marginBottom: '12px' }}>
      <h2 style={{ fontSize: '1.2rem' }}>Technical, news or both?</h2>
      <p style={{ fontSize: '13px', color: '#546e7a', margin: '4px 0 10px', lineHeight: 1.45 }}>
        The three Buy/Sell signals<InfoTip term="combinedSignal" /> against simply holding, for each instrument over the same dates, starting when its news begins.
        Their rules were fixed before seeing these results.
      </p>

      <PlainSummary points={describeNewsTests(results, cost)} />

      {tested.length > 0 && (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
            <thead>
              <tr>
                <th style={{ ...headerStyle, textAlign: 'left' }}>Return per year</th>
                <th style={headerStyle}>Years</th>
                <th style={headerStyle}>News coverage<InfoTip title="News coverage" text="The share of tested days with at least 3 stories about this instrument in the past week. On other days the news signal says Hold." /></th>
                <th style={headerStyle}>Holding</th>
                <th style={headerStyle}>Technical</th>
                <th style={headerStyle}>News<InfoTip term="newsSignal" /></th>
                <th style={headerStyle}>Combined<InfoTip term="combinedSignal" /></th>
              </tr>
            </thead>
            <tbody>
              {tested.map((r) => (
                <tr
                  key={r.symbol}
                  onClick={() => onSelect(r.symbol)}
                  style={{ cursor: 'pointer', backgroundColor: shown?.symbol === r.symbol ? '#e8f1fb' : undefined }}
                >
                  <td style={{ ...cellStyle, textAlign: 'left' }}>
                    <strong style={{ color: COLORS.link }}>{r.symbol}</strong>
                    {r.newsTest.years < MIN_RELIABLE_YEARS && <div style={{ fontSize: '12px', color: COLORS.down }}>too short to judge</div>}
                  </td>
                  <td style={cellStyle}>{r.newsTest.years.toFixed(1)}</td>
                  <td style={cellStyle}>{r.newsTest.coveragePercent.toFixed(0)}%</td>
                  <td style={cellStyle}>
                    <strong>{formatPercent(r.newsTest.holding.annualReturn, 1)}</strong>
                    <div style={{ fontSize: '12px', color: COLORS.muted }}>fall {r.newsTest.holding.maxDrawdown.toFixed(0)}%</div>
                  </td>
                  <RuleCell result={r} ruleKey="technical" />
                  <RuleCell result={r} ruleKey="news" />
                  <RuleCell result={r} ruleKey="combined" />
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      )}
      {untested.length > 0 && (
        <p style={{ fontSize: '13px', color: COLORS.muted, marginTop: '8px' }}>Not enough news history to test: {untested.join(', ')}.</p>
      )}

      {shown && curve && (
        <>
          <h3 style={{ fontSize: '1rem', marginTop: '16px' }}>
            {shown.symbol}: what 100 would have become, from {formatDate(shown.newsTest.startDate!)}
          </h3>
          <div style={{ width: '100%', height: '300px', marginTop: '8px' }}>
            <ResponsiveContainer width="100%" height="100%">
              <LineChart data={curve} margin={{ top: 5, right: 20, left: 0, bottom: 5 }}>
                <CartesianGrid strokeDasharray="3 3" />
                <XAxis dataKey="date" minTickGap={50} />
                <YAxis domain={['auto', 'auto']} tickFormatter={(value: number) => value.toLocaleString()} />
                <Tooltip formatter={(value) => (typeof value === 'number' ? value.toFixed(1) : value)} />
                <Legend />
                <ReferenceLine y={100} stroke="#999" />
                <Line type="monotone" dataKey="Simply holding" stroke="#000000" strokeWidth={2} dot={false} isAnimationActive={false} />
                <Line type="monotone" dataKey="Technical" stroke="#1976d2" strokeWidth={1.5} dot={false} isAnimationActive={false} />
                <Line type="monotone" dataKey="News" stroke="#ef6c00" strokeWidth={1.5} dot={false} isAnimationActive={false} />
                <Line type="monotone" dataKey="Combined" stroke="#7b1fa2" strokeWidth={2} dot={false} isAnimationActive={false} />
              </LineChart>
            </ResponsiveContainer>
          </div>
        </>
      )}

      <p style={{ fontSize: '13px', color: '#546e7a', marginTop: '10px', lineHeight: 1.45 }}>
        Green is more than 1 point a year ahead of holding, red more than 1 point behind, grey about the same.{' '}
        <strong>Limits of this news test:</strong> older news comes from a search that returns only a few stories per week, so it is a sample, not all the news.
        Sentiment is read automatically from headline wording. Many headlines report moves that have already happened, so news often lags the price.
        The stock funds and KMI-30 mostly share the KSE-100's news, and the gold fund shares gold's, so their results aren't independent.
      </p>
    </section>
  )
}
