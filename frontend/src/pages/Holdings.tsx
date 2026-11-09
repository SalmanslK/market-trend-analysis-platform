import { useEffect, useState, useMemo } from 'react'
import { Link } from 'react-router-dom'
import { analyticsApi } from '../services/api/analyticsApi'
import { InstrumentAnalyticsDto, InstrumentType } from '../services/types/marketData'
import { changeColor, COLORS, formatDate, formatNumber, formatPercent } from '../services/format'
import ScaledSignalBadge from '../components/signals/ScaledSignalBadge'
import TrendBadge from '../components/trend/TrendBadge'
import InfoTip from '../components/help/InfoTip'
import { applySignalMode, useSignalMode } from '../services/signalMode'
import { useFeatures } from '../services/features'
import PlainSummary from '../components/help/PlainSummary'

export interface Holding {
  symbol: string
  units: number
  /** Price paid per unit, optional */
  buyPrice?: number
  /** yyyy-mm-dd, optional; payouts after it are counted */
  buyDate?: string
}

const STORAGE_KEY = 'holdings'
// Indices can't be bought directly; their index funds (e.g. KMIF for KMI-30) can
const NOT_OWNABLE = ['KSE100', 'KMI30']
// Priced in US dollars (gold per ounce); converted to rupees at the latest USD/PKR rate
const USD_PRICED = ['GOLDINT']

function loadHoldings(): Holding[] {
  try {
    const saved = localStorage.getItem(STORAGE_KEY)
    return saved ? JSON.parse(saved) : []
  } catch {
    return []
  }
}

function saveHoldings(holdings: Holding[]) {
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(holdings))
  } catch {
    // Only a convenience: the page still works for this visit
  }
}

/** Stocks, gold or cash, for the split of what you own. */
function kind(a: InstrumentAnalyticsDto): 'Stocks' | 'Gold' | 'Cash' | 'Other' {
  if (a.isCash) return 'Cash'
  if (a.symbol === 'MGF' || a.symbol.startsWith('GOLD')) return 'Gold'
  if (a.type === InstrumentType.StockIndex || a.type === InstrumentType.MutualFund) return 'Stocks'
  return 'Other'
}

const cellStyle = { padding: '8px 10px', borderTop: `1px solid ${COLORS.border}`, textAlign: 'right' as const, whiteSpace: 'nowrap' as const }
const headerStyle = { padding: '8px 10px', textAlign: 'right' as const, fontSize: '13px', color: COLORS.muted, whiteSpace: 'nowrap' as const }
const inputStyle = { width: '100%', padding: '6px', fontSize: '15px', boxSizing: 'border-box' as const }

export default function Holdings() {
  const [holdings, setHoldings] = useState<Holding[]>(loadHoldings)
  const [loaded, setAnalytics] = useState<InstrumentAnalyticsDto[]>([])
  const mode = useSignalMode()
  const { news } = useFeatures()
  const analytics = useMemo(() => loaded.map((a) => applySignalMode(a, mode, news)), [loaded, mode, news])
  const [draft, setDraft] = useState<{ symbol: string; units: string; buyPrice: string; buyDate: string }>({ symbol: '', units: '', buyPrice: '', buyDate: '' })
  const [error, setError] = useState<string | null>(null)
  // Show "enter the units" only after a first try, not while the person is still filling in the form
  const [triedToAdd, setTriedToAdd] = useState(false)
  // The last removed holding, so a mistaken removal can be undone
  const [removed, setRemoved] = useState<Holding | null>(null)
  const [saved, setSaved] = useState<string | null>(null)

  useEffect(() => {
    analyticsApi.getAll()
      .then((result) => setAnalytics(result))
      .catch(() => setError('Could not load prices. Check that the API is running.'))
  }, [])

  const update = (next: Holding[]) => {
    setHoldings(next)
    saveHoldings(next)
  }

  const ownable = analytics.filter((a) => !NOT_OWNABLE.includes(a.symbol))
  const bySymbol = new Map(analytics.map((a) => [a.symbol, a]))

  const add = () => {
    const units = Number(draft.units)
    setTriedToAdd(true)
    if (!draft.symbol || !(units > 0)) return
    const holding: Holding = {
      symbol: draft.symbol,
      units,
      buyPrice: Number(draft.buyPrice) > 0 ? Number(draft.buyPrice) : undefined,
      buyDate: draft.buyDate || undefined,
    }
    const replacing = holdings.some((h) => h.symbol === holding.symbol)
    update([...holdings.filter((h) => h.symbol !== holding.symbol), holding])
    setDraft({ symbol: '', units: '', buyPrice: '', buyDate: '' })
    setTriedToAdd(false)
    setRemoved(null)
    setSaved(`${replacing ? 'Updated' : 'Added'} ${holding.symbol}.`)
  }

  const remove = (holding: Holding) => {
    update(holdings.filter((x) => x.symbol !== holding.symbol))
    setRemoved(holding)
    setSaved(null)
  }

  const undoRemove = () => {
    if (!removed) return
    update([...holdings, removed])
    setSaved(`Restored ${removed.symbol}.`)
    setRemoved(null)
  }

  const unitsProblem = !draft.symbol
    ? 'Choose an investment first.'
    : !draft.units.trim()
      ? 'Enter how many units you own.'
      : !(Number(draft.units) > 0)
        ? 'Units must be a number above 0.'
        : null
  const alreadyHeld = holdings.some((h) => h.symbol === draft.symbol)

  const usdPkr = bySymbol.get('USDPKR')?.lastPrice
  const rows = holdings
    .map((h) => {
      const a = bySymbol.get(h.symbol)
      if (!a) return null
      const inDollars = USD_PRICED.includes(a.symbol)
      if (inDollars && !usdPkr) return null
      // Rupees per unit of the instrument's own price (1 for rupee prices)
      const toRupees = inDollars ? usdPkr! : 1
      const value = h.units * a.lastPrice * toRupees
      // The price paid is in the same currency as the price; dollar prices use today's rate, so the gain is the dollar gain
      const cost = h.buyPrice ? h.units * h.buyPrice * toRupees : undefined
      // Payouts after the buy date (all listed payouts when no date), in rupees for these units
      const payouts = a.payouts.filter((p) => !h.buyDate || p.date.slice(0, 10) > h.buyDate)
      const paidOut = payouts.reduce((sum, p) => sum + p.amountPerUnit * h.units, 0)
      return { h, a, value, cost, paidOut, payoutCount: payouts.length, inDollars }
    })
    .filter((r): r is NonNullable<typeof r> => r != null)

  const total = rows.reduce((sum, r) => sum + r.value, 0)
  const totalCost = rows.every((r) => r.cost != null) && rows.length > 0 ? rows.reduce((sum, r) => sum + (r.cost ?? 0), 0) : undefined
  const totalPaidOut = rows.reduce((sum, r) => sum + r.paidOut, 0)
  const split = (['Stocks', 'Gold', 'Cash', 'Other'] as const)
    .map((k) => ({ kind: k, value: rows.filter((r) => kind(r.a) === k).reduce((sum, r) => sum + r.value, 0) }))
    .filter((s) => s.value > 0)
  // The same split as weights, to test on the Portfolio page
  const mixParam = rows
    .filter((r) => !['USDPKR', 'GOLDINT'].includes(r.h.symbol) && total > 0)
    .map((r) => `${r.h.symbol}:${Math.round((r.value / total) * 100)}`)
    .join(',')

  const summary: string[] = []
  if (rows.length > 0) {
    summary.push(`Your holdings are worth Rs ${formatNumber(total, 0)} at the latest prices.`)
    if (totalCost != null && totalCost > 0)
      summary.push(`That is ${formatPercent((total / totalCost - 1) * 100, 1)} against the Rs ${formatNumber(totalCost, 0)} you paid${totalPaidOut > 0 ? `, plus Rs ${formatNumber(totalPaidOut, 0)} paid out to you` : ''}.`)
    else if (totalPaidOut > 0)
      summary.push(`The funds paid out Rs ${formatNumber(totalPaidOut, 0)} on these units${rows.some((r) => r.h.buyDate) ? ' since you bought them' : ' over the listed payouts'}.`)
    summary.push(`Split: ${split.map((s) => `${s.kind.toLowerCase()} ${((s.value / total) * 100).toFixed(0)}%`).join(', ')}.`)
    const selling = rows.filter((r) => !r.a.isCash && r.a.combinedSignal.action.includes('Sell')).map((r) => r.h.symbol)
    const down = rows.filter((r) => !r.a.isCash && r.a.longTermTrend.direction === 'Down' && r.a.longTermTrend.phase !== 'Recovering').map((r) => r.h.symbol)
    if (selling.length) summary.push(`The ${news ? 'combined' : 'technical'} signal says Sell for ${selling.join(', ')}. Signals reduced falls in testing but earned less than holding; see Track Record.`)
    if (down.length) summary.push(`The long-term trend is down for ${down.join(', ')}.`)
  }

  return (
    <div style={{ padding: '20px' }}>
      <h1>My Holdings</h1>
      <p style={{ color: COLORS.muted, margin: '6px 0 20px' }}>
        Enter what you own to see its value, the payouts you received and what the signals say. Saved only in this browser; nothing is sent anywhere.
      </p>

      {error && <div style={{ color: COLORS.down, marginBottom: '12px' }}>{error}</div>}

      {summary.length > 0 && (
        <PlainSummary
          points={summary}
          footer={mixParam && <>See how this split did in the past on the <Link to={`/portfolio?mix=${mixParam}`} style={{ color: COLORS.link }}>Portfolio page</Link>.</>}
        />
      )}

      <section style={{ padding: '16px 20px', backgroundColor: 'white', borderRadius: '6px', margin: '16px 0' }}>
        <h2 style={{ fontSize: '1.15rem', marginBottom: '8px' }}>Add or update a holding</h2>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(140px, 1fr))', gap: '8px', alignItems: 'end' }}>
          <label style={{ fontSize: '13px', color: COLORS.muted }}>
            Investment
            <select value={draft.symbol} onChange={(e) => setDraft({ ...draft, symbol: e.target.value })} style={inputStyle}>
              <option value="">Choose...</option>
              {ownable.map((a) => <option key={a.symbol} value={a.symbol}>{a.symbol} - {a.name}</option>)}
            </select>
          </label>
          <label style={{ fontSize: '13px', color: COLORS.muted }}>
            Units
            <input type="number" inputMode="decimal" min={0} value={draft.units} onChange={(e) => setDraft({ ...draft, units: e.target.value })} style={inputStyle} />
          </label>
          <label style={{ fontSize: '13px', color: COLORS.muted }}>
            Price paid per unit (optional)
            <input type="number" inputMode="decimal" min={0} value={draft.buyPrice} onChange={(e) => setDraft({ ...draft, buyPrice: e.target.value })} style={inputStyle} />
          </label>
          <label style={{ fontSize: '13px', color: COLORS.muted }}>
            Bought on (optional)
            <input type="date" value={draft.buyDate} onChange={(e) => setDraft({ ...draft, buyDate: e.target.value })} style={inputStyle} />
          </label>
          <button
            type="button"
            onClick={add}
            style={{ padding: '8px 16px', backgroundColor: COLORS.link, color: 'white', border: 'none', borderRadius: '6px', fontSize: '14px', height: '36px' }}
          >
            {alreadyHeld ? 'Update holding' : 'Add to my holdings'}
          </button>
        </div>
        {triedToAdd && unitsProblem && (
          <p role="alert" style={{ color: COLORS.down, fontSize: '13px', marginTop: '8px' }}>{unitsProblem}</p>
        )}
        {alreadyHeld && !unitsProblem && (
          <p style={{ color: COLORS.muted, fontSize: '13px', marginTop: '8px' }}>You already hold {draft.symbol}: saving replaces its units, price and date.</p>
        )}
        <p style={{ color: COLORS.muted, fontSize: '12px', marginTop: '8px' }}>
          Units: fund units, tolas of gold (GOLDPK), ounces (GOLDINT) or US dollars (USDPKR). Enter the price paid in the same currency as the price: US dollars for GOLDINT, whose value is converted to rupees at the latest USD/PKR rate.
        </p>
      </section>

      {rows.length > 0 && (
        <div style={{ overflowX: 'auto', backgroundColor: 'white', borderRadius: '6px' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
            <thead>
              <tr>
                <th style={{ ...headerStyle, textAlign: 'left' }}>Investment</th>
                <th style={headerStyle}>Units</th>
                <th style={headerStyle}>Price</th>
                <th style={headerStyle}>Value (Rs)</th>
                <th style={headerStyle}>Gain</th>
                <th style={headerStyle}>Paid out (Rs)<InfoTip term="payouts" /></th>
                <th style={{ ...headerStyle, textAlign: 'center' }}>{news ? <>Combined<InfoTip term="combinedSignal" /></> : <>Technical<InfoTip term="signal" /></>}</th>
                <th style={{ ...headerStyle, textAlign: 'center' }}>Trend<InfoTip term="longTermTrend" /></th>
                <th style={headerStyle}></th>
              </tr>
            </thead>
            <tbody>
              {rows.map(({ h, a, value, cost, paidOut, payoutCount, inDollars }) => (
                <tr key={h.symbol}>
                  <td style={{ ...cellStyle, textAlign: 'left' }}>
                    <Link to={`/instrument/${a.symbol}`} style={{ color: COLORS.link, textDecoration: 'none', fontWeight: 'bold' }}>{a.symbol}</Link>
                    <div style={{ fontSize: '12px', color: COLORS.muted }}>{a.name}{h.buyDate ? `, bought ${formatDate(h.buyDate)}` : ''}</div>
                  </td>
                  <td style={cellStyle}>{formatNumber(h.units, h.units % 1 ? 2 : 0)}</td>
                  <td style={cellStyle}>{inDollars ? `$${formatNumber(a.lastPrice)}` : formatNumber(a.lastPrice)}</td>
                  <td style={{ ...cellStyle, fontWeight: 'bold' }}>{formatNumber(value, 0)}</td>
                  <td style={{ ...cellStyle, color: changeColor(cost ? value - cost : undefined) }}>
                    {cost ? formatPercent((value / cost - 1) * 100, 1) : '-'}
                  </td>
                  <td style={cellStyle}>{paidOut > 0 ? `${formatNumber(paidOut, 0)} (${payoutCount})` : '-'}</td>
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    {a.isCash ? <span style={{ color: COLORS.muted }}>{(a.lowRiskKind ?? 'cash fund').toLowerCase()}</span> : <ScaledSignalBadge signal={a.combinedSignal} />}
                  </td>
                  <td style={{ ...cellStyle, textAlign: 'center' }}>
                    {a.isCash ? '' : <TrendBadge trend={a.longTermTrend} />}
                  </td>
                  <td style={cellStyle}>
                    <button
                      type="button"
                      onClick={() => remove(h)}
                      aria-label={`Remove ${h.symbol}`}
                      style={{ border: 'none', background: 'none', color: COLORS.down, cursor: 'pointer', fontSize: '16px' }}
                    >
                      ✕
                    </button>
                  </td>
                </tr>
              ))}
              <tr>
                <td style={{ ...cellStyle, textAlign: 'left', fontWeight: 'bold' }}>Total</td>
                <td style={cellStyle} colSpan={2}></td>
                <td style={{ ...cellStyle, fontWeight: 'bold' }}>{formatNumber(total, 0)}</td>
                <td style={{ ...cellStyle, color: changeColor(totalCost ? total - totalCost : undefined) }}>
                  {totalCost ? formatPercent((total / totalCost - 1) * 100, 1) : '-'}
                </td>
                <td style={cellStyle}>{totalPaidOut > 0 ? formatNumber(totalPaidOut, 0) : '-'}</td>
                <td style={cellStyle} colSpan={3}></td>
              </tr>
            </tbody>
          </table>
        </div>
      )}

      {(saved || removed) && (
        <div role="status" style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '10px', padding: '8px 12px', marginBottom: '12px', backgroundColor: removed ? '#fff3e0' : '#e8f5e9', borderRadius: '6px', fontSize: '14px' }}>
          {removed ? <>Removed {removed.symbol}.</> : saved}
          {removed && (
            <button type="button" onClick={undoRemove} style={{ border: `1px solid ${COLORS.border}`, background: 'white', borderRadius: '6px', padding: '3px 10px', color: COLORS.link }}>
              Undo
            </button>
          )}
        </div>
      )}

      {rows.length === 0 && analytics.length > 0 && (
        <div style={{ padding: '20px', backgroundColor: 'white', borderRadius: '6px', lineHeight: 1.5 }}>
          <strong>No holdings yet.</strong>
          <ol style={{ margin: '6px 0 0', paddingLeft: '20px', color: COLORS.neutral }}>
            <li>Choose an investment above, for example MIF (Meezan Islamic Fund).</li>
            <li>Enter how many units you own. The price paid and date are optional, for your gain and payouts.</li>
            <li>Add it. Your list is saved in this browser only.</li>
          </ol>
        </div>
      )}

      <ul style={{ color: COLORS.muted, fontSize: '13px', lineHeight: 1.5, marginTop: '16px', paddingLeft: '20px' }}>
        <li>Value uses the latest stored price. "Paid out" is the fund payouts per unit times your units; if you reinvested them, your unit count already includes them.</li>
        <li>Gain compares today's value with the price you paid, without payouts. KSE-100 and KMI-30 aren't listed because they are indices; their index funds (e.g. KMIF) can be held.</li>
        <li>Signals describe past prices{news ? ' and news' : ''}. They aren't financial advice.</li>
      </ul>
    </div>
  )
}
