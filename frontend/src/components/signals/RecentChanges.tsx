import { useEffect, useState } from 'react'
import { Link } from 'react-router-dom'
import { alertsApi } from '../../services/api/alertsApi'
import { SignalAlert, SignalAction } from '../../services/types/marketData'
import { COLORS, formatDate, signalStyles } from '../../services/format'
import InfoTip from '../help/InfoTip'
import { useSignalMode } from '../../services/signalMode'

const NOTIFY_KEY = 'alerts.notify'
const SEEN_KEY = 'alerts.lastSeenDate'
// The Worker checks prices every 6 hours, so checking every 30 minutes catches changes soon after they appear
const CHECK_EVERY_MS = 30 * 60 * 1000
const SHOWN_AT_FIRST = 8

function read(key: string): string | null {
  try {
    return localStorage.getItem(key)
  } catch {
    return null
  }
}

function write(key: string, value: string) {
  try {
    localStorage.setItem(key, value)
  } catch {
    // Only a convenience
  }
}

const canNotify = () => typeof window !== 'undefined' && 'Notification' in window

function label(value: string): string {
  if (value === 'Up') return '▲ Up'
  if (value === 'Down') return '▼ Down'
  return signalStyles[value as SignalAction]?.label ?? value
}

/** One line in plain words, e.g. "KSE100: combined signal turned Buy (was Hold)" */
function describeAlert(a: SignalAlert): string {
  const what = a.signal === 'Trend' ? 'long-term trend turned' : `${a.signal.toLowerCase()} signal turned`
  return `${a.symbol}: ${what} ${label(a.to)} (was ${label(a.from)})`
}

/**
 * Signal and trend changes of the last 30 days, important ones first, with optional browser notifications
 * for new important changes while the app is open.
 */
export default function RecentChanges() {
  // Technical and combined changes follow the signal chosen at the top of the page
  const mode = useSignalMode()
  const [alerts, setAlerts] = useState<SignalAlert[]>([])
  const [showAll, setShowAll] = useState(false)
  const [expanded, setExpanded] = useState(false)
  const [notify, setNotify] = useState(() => read(NOTIFY_KEY) === 'on' && canNotify() && Notification.permission === 'granted')
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    const check = () => alertsApi.getRecent(30, mode)
      .then((result) => {
        if (cancelled) return
        setAlerts(result)
        setError(null)
        announce(result)
      })
      .catch(() => !cancelled && setError('Could not load recent changes.'))

    check()
    const timer = window.setInterval(check, CHECK_EVERY_MS)
    return () => {
      cancelled = true
      window.clearInterval(timer)
    }
  }, [mode])

  const toggleNotify = async () => {
    if (notify) {
      write(NOTIFY_KEY, 'off')
      setNotify(false)
      return
    }
    if (!canNotify()) return
    const permission = Notification.permission === 'granted' ? 'granted' : await Notification.requestPermission()
    if (permission !== 'granted') return
    write(NOTIFY_KEY, 'on')
    // Only changes from now on are announced
    const newest = alerts[0]?.date
    if (newest) write(SEEN_KEY, newest)
    setNotify(true)
  }

  const visible = alerts.filter((a) => showAll || a.important)
  const listed = expanded ? visible : visible.slice(0, SHOWN_AT_FIRST)

  return (
    <section style={{ padding: '16px 20px', backgroundColor: 'white', borderRadius: '6px', marginTop: '20px' }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'center', gap: '8px' }}>
        <h2 style={{ fontSize: '1.15rem' }}>
          What changed in the last 30 days
          <InfoTip
            title="Recent changes"
            text="Days a signal or the long-term trend changed side, worked out from each day's close with the same rules as the signals above. Important changes are the combined signal turning Buy or Sell, and the trend turning. A change to Hold means stay as you are."
          />
        </h2>
        <div style={{ display: 'flex', flexWrap: 'wrap', gap: '12px', fontSize: '13px' }}>
          <label style={{ cursor: 'pointer' }}>
            <input type="checkbox" checked={showAll} onChange={(e) => setShowAll(e.target.checked)} /> Show all changes
          </label>
          {canNotify() && (
            <button
              type="button"
              onClick={toggleNotify}
              style={{ padding: '4px 10px', border: `1px solid ${COLORS.border}`, borderRadius: '6px', backgroundColor: notify ? '#e8f5e9' : 'white', cursor: 'pointer', fontSize: '13px' }}
            >
              {notify ? '🔔 Notifying you' : '🔕 Notify me of important changes'}
            </button>
          )}
        </div>
      </div>
      {notify && (
        <p style={{ color: COLORS.muted, fontSize: '12px', marginTop: '4px' }}>
          Notifications appear while this app is open in a browser tab.
        </p>
      )}

      {error && <p style={{ color: COLORS.down, marginTop: '8px' }}>{error}</p>}
      {!error && visible.length === 0 && (
        <p style={{ color: COLORS.muted, marginTop: '8px' }}>{showAll ? 'No changes in the last 30 days.' : 'No important changes in the last 30 days.'}</p>
      )}

      <ul style={{ listStyle: 'none', marginTop: '8px' }}>
        {listed.map((a) => (
          <li key={`${a.date}-${a.symbol}-${a.signal}`} style={{ display: 'flex', flexWrap: 'wrap', gap: '4px 12px', padding: '6px 0', borderTop: `1px solid ${COLORS.border}`, fontSize: '14px' }}>
            <span style={{ color: COLORS.muted, minWidth: '90px' }}>{formatDate(a.date)}</span>
            <span style={{ fontWeight: a.important ? 'bold' : 'normal' }}>
              <Link to={`/instrument/${a.symbol}`} style={{ color: COLORS.link, textDecoration: 'none' }}>{a.symbol}</Link>
              {': '}{a.signal === 'Trend' ? 'long-term trend' : `${a.signal.toLowerCase()} signal`} turned{' '}
              <span style={{ color: a.to === 'Up' || a.to.includes('Buy') ? COLORS.up : a.to === 'Down' || a.to.includes('Sell') ? COLORS.down : COLORS.neutral }}>
                {label(a.to)}
              </span>
              <span style={{ color: COLORS.muted, fontWeight: 'normal' }}> (was {label(a.from)})</span>
            </span>
          </li>
        ))}
      </ul>
      {visible.length > SHOWN_AT_FIRST && (
        <button type="button" onClick={() => setExpanded(!expanded)} style={{ marginTop: '6px', border: 'none', background: 'none', color: COLORS.link, cursor: 'pointer', padding: 0, fontSize: '14px' }}>
          {expanded ? 'Show fewer' : `Show all ${visible.length}`}
        </button>
      )}
    </section>
  )
}

/** Notifies about important changes newer than the last one announced, when the viewer asked for it. */
function announce(alerts: SignalAlert[]) {
  if (read(NOTIFY_KEY) !== 'on' || !canNotify() || Notification.permission !== 'granted') return
  const seen = read(SEEN_KEY) ?? ''
  const fresh = alerts.filter((a) => a.important && a.date > seen)
  if (fresh.length === 0) return
  write(SEEN_KEY, alerts[0].date)
  const body = fresh.slice(0, 4).map(describeAlert).join('\n') + (fresh.length > 4 ? `\nand ${fresh.length - 4} more` : '')
  try {
    new Notification('Market Analytics: signals changed', { body, tag: 'market-analytics-alerts' })
  } catch {
    // Some browsers only allow notifications from a service worker; the list on the page still shows the changes
  }
}
