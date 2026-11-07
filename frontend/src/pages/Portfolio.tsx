import { useEffect, useMemo, useState } from 'react'
import { CartesianGrid, Legend, Line, LineChart, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import { portfolioApi } from '../services/api/portfolioApi'
import { PortfolioComparison, PortfolioMix, PortfolioResult, RebalanceSchedule } from '../services/types/marketData'
import { changeColor, COLORS, formatDate, formatPercent } from '../services/format'
import { describePortfolios } from '../services/plainLanguage'
import InfoTip from '../components/help/InfoTip'
import PlainSummary from '../components/help/PlainSummary'

// Prices that can't be bought directly in rupees as they are: an exchange rate and gold priced in dollars
const NOT_INVESTABLE = ['USDPKR', 'GOLDINT']
const COSTS = [0, 0.25, 0.5, 1, 2]
const SCHEDULES: { value: RebalanceSchedule; label: string }[] = [
  { value: 'Never', label: 'Never' },
  { value: 'Yearly', label: 'Every year' },
  { value: 'HalfYearly', label: 'Every 6 months' },
  { value: 'Quarterly', label: 'Every 3 months' },
]
const LINE_COLORS = ['#1565c0', '#2e7d32', '#ef6c00', '#6a1b9a', '#c62828', '#00838f', '#5d4037', '#455a64', '#ad1457', '#827717']
const STORAGE_KEY = 'portfolio.myMix'
const DEFAULT_MIX: PortfolioMix = { name: 'My mix', weights: { MIF: 60, MGF: 20, MCF: 20 }, rebalance: 'Yearly', trendFilter: false }

function loadMix(): PortfolioMix {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    return saved ? { ...DEFAULT_MIX, ...JSON.parse(saved) } : DEFAULT_MIX
  } catch {
    return DEFAULT_MIX
  }
}

function saveMix(mix: PortfolioMix) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(mix))
  } catch {
    // Saving is only a convenience
  }
}

const cellStyle = { padding: '8px 10px', borderTop: `1px solid ${COLORS.border}`, textAlign: 'right' as const, whiteSpace: 'nowrap' as const }
const headerStyle = { padding: '8px 10px', textAlign: 'right' as const, fontSize: '13px', color: COLORS.muted, whiteSpace: 'nowrap' as const }

export default function Portfolio() {
  const [presets, setPresets] = useState<PortfolioMix[]>([])
  const [mix, setMix] = useState<PortfolioMix>(loadMix)
  const [tested, setTested] = useState<PortfolioMix | null>(null)
  const [cost, setCost] = useState(0.5)
  const [comparison, setComparison] = useState<PortfolioComparison | null>(null)
  const [shown, setShown] = useState<string[]>([])
  const [loading, setLoading] = useState(true)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    portfolioApi.getPresets().then(setPresets).catch(() => setError('Could not load the mixes. Check that the API is running.'))
  }, [])

  useEffect(() => {
    if (presets.length === 0) return
    const mixes = tested ? [tested, ...presets] : presets
    setLoading(true)
    portfolioApi.compare(mixes, cost)
      .then((result) => {
        setComparison(result)
        setError(null)
        setShown((current) => {
          const names = result.results.map((r) => r.name)
          const kept = current.filter((n) => names.includes(n))
          const wanted = tested ? [tested.name] : []
          return [...new Set([...wanted, ...(kept.length ? kept : names.slice(0, 3)), names[names.length - 1] ?? ''])].filter(Boolean)
        })
      })
      .catch(() => setError('Could not test the mixes. Check that the API is running.'))
      .finally(() => setLoading(false))
  }, [presets, tested, cost])

  const instruments = (comparison?.instruments ?? []).filter((i) => !NOT_INVESTABLE.includes(i.symbol))
  const total = Object.values(mix.weights).reduce((sum, w) => sum + (w || 0), 0)

  const setWeight = (symbol: string, value: number) => {
    const weights = { ...mix.weights, [symbol]: Math.max(0, Math.min(100, value || 0)) }
    if (!weights[symbol]) delete weights[symbol]
    setMix({ ...mix, weights })
  }

  const test = () => {
    const named = { ...mix, name: mix.name.trim() || 'My mix' }
    saveMix(named)
    setTested(named)
  }

  const chartData = useMemo(() => {
    if (!comparison) return []
    const byDate = new Map<string, Record<string, number | string>>()
    for (const r of comparison.results.filter((r) => shown.includes(r.name))) {
      for (const point of r.equityCurve) {
        const key = point.date.slice(0, 10)
        const row = byDate.get(key) ?? { date: key }
        row[r.name] = point.value
        byDate.set(key, row)
      }
    }
    return [...byDate.values()].sort((a, b) => String(a.date).localeCompare(String(b.date)))
  }, [comparison, shown])

  const toggle = (name: string) => setShown(shown.includes(name) ? shown.filter((n) => n !== name) : [...shown, name])
  const colorOf = (name: string) => LINE_COLORS[(comparison?.results.findIndex((r) => r.name === name) ?? 0) % LINE_COLORS.length]
  const shownResults = comparison?.results.filter((r) => shown.includes(r.name)) ?? []
  const years = [...new Set(shownResults.flatMap((r) => r.yearlyReturns.map((y) => y.year)))].sort()

  return (
    <div style={{ padding: '20px' }}>
      <h1>Portfolio Mix</h1>
      <p style={{ color: COLORS.muted, margin: '6px 0 20px' }}>
        What if you had split your money between stocks, gold and cash? This replays mixes over past prices.<InfoTip term="portfolio" />
      </p>

      {comparison && comparison.results.length > 0 && (
        <PlainSummary
          points={describePortfolios(comparison)}
          footer="Past results don't guarantee future ones. This isn't financial advice: think about your own goals and how much of a fall you could live with."
        />
      )}

      <section style={{ padding: '16px 20px', backgroundColor: 'white', borderRadius: '6px', margin: '16px 0' }}>
        <h2 style={{ fontSize: '1.15rem', marginBottom: '8px' }}>Build your own mix</h2>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))', gap: '8px' }}>
          {instruments.map((i) => (
            <label key={i.symbol} style={{ display: 'block', padding: '8px 10px', border: `1px solid ${COLORS.border}`, borderRadius: '6px' }}>
              <div style={{ fontWeight: 'bold', fontSize: '14px' }}>
                {i.symbol}{i.isCash && <InfoTip term="cashFund" />}
              </div>
              <div style={{ fontSize: '12px', color: COLORS.muted, minHeight: '30px' }}>{i.name}, from {new Date(i.firstDate).getFullYear()}</div>
              <span style={{ display: 'flex', alignItems: 'center', gap: '4px', marginTop: '4px' }}>
                <input
                  type="number"
                  inputMode="numeric"
                  min={0}
                  max={100}
                  step={5}
                  value={mix.weights[i.symbol] ?? ''}
                  placeholder="0"
                  onChange={(e) => setWeight(i.symbol, Number(e.target.value))}
                  style={{ width: '70px', padding: '6px', fontSize: '15px' }}
                  aria-label={`${i.symbol} percent`}
                />
                %
              </span>
            </label>
          ))}
        </div>

        <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '12px 20px', marginTop: '12px', fontSize: '14px' }}>
          <span style={{ color: total > 100 ? COLORS.down : COLORS.neutral }}>
            Total {total}%{total < 100 ? `, the other ${100 - total}% in cash` : total > 100 ? ', will be scaled down to 100%' : ''}
          </span>
          <label>
            Rebalance<InfoTip term="rebalance" />{' '}
            <select value={mix.rebalance} onChange={(e) => setMix({ ...mix, rebalance: e.target.value as RebalanceSchedule })} style={{ padding: '4px' }}>
              {SCHEDULES.map((s) => <option key={s.value} value={s.value}>{s.label}</option>)}
            </select>
          </label>
          <label style={{ cursor: 'pointer' }}>
            <input type="checkbox" checked={mix.trendFilter} onChange={(e) => setMix({ ...mix, trendFilter: e.target.checked })} />{' '}
            Trend filter<InfoTip term="trendFilter" />
          </label>
          <label>
            Name{' '}
            <input value={mix.name} onChange={(e) => setMix({ ...mix, name: e.target.value })} maxLength={40} style={{ padding: '4px', width: '130px' }} />
          </label>
          <button
            type="button"
            onClick={test}
            disabled={total === 0 || loading}
            style={{ padding: '8px 16px', backgroundColor: COLORS.link, color: 'white', border: 'none', borderRadius: '6px', cursor: 'pointer', fontSize: '14px', opacity: total === 0 ? 0.5 : 1 }}
          >
            Test my mix
          </button>
        </div>
      </section>

      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '12px', marginBottom: '12px', fontSize: '14px' }}>
        <label>
          Cost per trade<InfoTip term="tradingCost" />{' '}
          <select value={cost} onChange={(e) => setCost(Number(e.target.value))} style={{ padding: '4px' }}>
            {COSTS.map((c) => <option key={c} value={c}>{c}%</option>)}
          </select>
        </label>
        {comparison && comparison.results.length > 0 && (
          <span style={{ color: COLORS.muted }}>
            All mixes compared from {formatDate(comparison.startDate)} to {formatDate(comparison.endDate)}
          </span>
        )}
        {loading && <span style={{ color: COLORS.muted }}>Testing...</span>}
      </div>

      {error && <div style={{ color: COLORS.down, marginBottom: '12px' }}>{error}</div>}

      {comparison && comparison.results.length > 0 && (
        <>
          <div style={{ overflowX: 'auto', backgroundColor: 'white', borderRadius: '6px' }}>
            <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
              <thead>
                <tr>
                  <th style={{ ...headerStyle, textAlign: 'left' }}>Show</th>
                  <th style={{ ...headerStyle, textAlign: 'left' }}>Mix</th>
                  <th style={headerStyle}>Return per year<InfoTip term="annualReturn" /></th>
                  <th style={headerStyle}>Worst fall<InfoTip term="drawdown" /></th>
                  <th style={headerStyle}>Volatility<InfoTip term="volatility" /></th>
                  <th style={headerStyle}>Sharpe<InfoTip term="sharpe" /></th>
                  <th style={headerStyle}>Trades</th>
                </tr>
              </thead>
              <tbody>
                {comparison.results.map((r) => (
                  <ResultRow key={r.name} result={r} mine={tested?.name === r.name} shown={shown.includes(r.name)} color={colorOf(r.name)} onToggle={() => toggle(r.name)} />
                ))}
              </tbody>
            </table>
          </div>

          {chartData.length > 0 && (
            <section style={{ padding: '16px 12px', backgroundColor: 'white', borderRadius: '6px', marginTop: '16px' }}>
              <h2 style={{ fontSize: '1.15rem', margin: '0 8px 8px' }}>Growth of 100</h2>
              <div style={{ height: '360px' }}>
                <ResponsiveContainer width="100%" height="100%">
                  <LineChart data={chartData} margin={{ top: 5, right: 16, left: 0, bottom: 5 }}>
                    <CartesianGrid strokeDasharray="3 3" stroke="#eeeeee" />
                    <XAxis dataKey="date" minTickGap={40} tickFormatter={(d: string) => d.slice(0, 7)} />
                    <YAxis domain={['auto', 'auto']} width={48} />
                    <Tooltip formatter={(v: number) => v.toFixed(1)} labelFormatter={(d: string) => formatDate(d)} />
                    <Legend />
                    {shownResults.map((r) => (
                      <Line key={r.name} type="monotone" dataKey={r.name} stroke={colorOf(r.name)} strokeWidth={tested?.name === r.name ? 2.5 : 1.5} dot={false} connectNulls isAnimationActive={false} />
                    ))}
                  </LineChart>
                </ResponsiveContainer>
              </div>
            </section>
          )}

          {shownResults.length > 0 && (
            <section style={{ padding: '16px 20px', backgroundColor: 'white', borderRadius: '6px', marginTop: '16px' }}>
              <h2 style={{ fontSize: '1.15rem', marginBottom: '4px' }}>Year by year</h2>
              <p style={{ color: COLORS.muted, fontSize: '13px', marginBottom: '8px' }}>The first and last years are partial.</p>
              <div style={{ overflowX: 'auto' }}>
                <table style={{ borderCollapse: 'collapse', fontSize: '14px' }}>
                  <thead>
                    <tr>
                      <th style={{ ...headerStyle, textAlign: 'left' }}>Mix</th>
                      {years.map((y) => <th key={y} style={headerStyle}>{y}</th>)}
                    </tr>
                  </thead>
                  <tbody>
                    {shownResults.map((r) => (
                      <tr key={r.name}>
                        <td style={{ ...cellStyle, textAlign: 'left', color: colorOf(r.name), fontWeight: 'bold' }}>{r.name}</td>
                        {years.map((y) => {
                          const value = r.yearlyReturns.find((x) => x.year === y)?.return
                          return <td key={y} style={{ ...cellStyle, color: changeColor(value) }}>{formatPercent(value, 1)}</td>
                        })}
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </section>
          )}
        </>
      )}

      <ul style={{ color: COLORS.muted, fontSize: '13px', lineHeight: 1.5, marginTop: '16px', paddingLeft: '20px' }}>
        <li>Fund returns count payouts as reinvested. Money not in the mix, or moved out by the trend filter, earns what Meezan Cash Fund earned.</li>
        <li>All mixes use the same dates: from when every chosen instrument has prices. Adding a fund with a short history shortens the comparison for all.</li>
        <li>Each buy and sell pays the cost above, except moves into and out of the cash fund. Rebalancing happens at the first close of each period.</li>
        <li>The preset mixes were fixed before seeing their results. MIF stands for stocks, MGF for gold and MCF for cash.</li>
        <li>Past results don't guarantee future ones: the period is short and was mostly a rising market for stocks and gold.</li>
      </ul>
    </div>
  )
}

function ResultRow({ result: r, mine, shown, color, onToggle }: { result: PortfolioResult; mine: boolean; shown: boolean; color: string; onToggle: () => void }) {
  const parts = Object.entries(r.weights).map(([s, w]) => `${s} ${w}%`).join(', ')
  const rest = 100 - Object.values(r.weights).reduce((a, b) => a + b, 0)
  const schedule = SCHEDULES.find((s) => s.value === r.rebalance)?.label.toLowerCase()
  return (
    <tr style={{ backgroundColor: mine ? '#e3f2fd' : undefined }}>
      <td style={{ ...cellStyle, textAlign: 'left' }}>
        <input type="checkbox" checked={shown} onChange={onToggle} aria-label={`Show ${r.name} on the chart`} style={{ accentColor: color }} />
      </td>
      <td style={{ ...cellStyle, textAlign: 'left', whiteSpace: 'normal', minWidth: '180px' }}>
        <strong>{r.name}</strong>
        <div style={{ fontSize: '12px', color: COLORS.muted }}>
          {parts}{rest > 0.01 ? `, cash ${rest.toFixed(0)}%` : ''}
          {Object.keys(r.weights).length > 1 ? ` · rebalanced ${schedule}` : ''}
          {r.trendFilter ? ' · trend filter' : ''}
        </div>
      </td>
      <td style={{ ...cellStyle, color: changeColor(r.annualReturn), fontWeight: 'bold' }}>{formatPercent(r.annualReturn, 1)}</td>
      <td style={{ ...cellStyle, color: changeColor(r.maxDrawdown) }}>{formatPercent(r.maxDrawdown, 1)}</td>
      <td style={cellStyle}>{r.volatility.toFixed(1)}%</td>
      <td style={cellStyle}>{r.sharpe != null ? r.sharpe.toFixed(2) : '-'}</td>
      <td style={cellStyle}>{r.trades}</td>
    </tr>
  )
}
