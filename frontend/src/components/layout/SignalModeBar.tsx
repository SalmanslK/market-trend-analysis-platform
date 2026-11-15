import { useLocation } from 'react-router-dom'
import { setSignalMode, SignalMode, useSignalMode } from '../../services/signalMode'
import { COLORS } from '../../services/format'
import { useFeatures } from '../../services/features'
import InfoTip from '../help/InfoTip'

// Pages that show Buy/Sell signals
const SIGNAL_PAGES = ['/', '/instrument', '/track-record', '/holdings']

const CHOICES: { mode: SignalMode; label: string }[] = [
  { mode: 'steady', label: 'Steady (20-day average)' },
  { mode: 'daily', label: 'Daily (today\'s 5 votes)' },
]

/** The choice of technical signal, at the top of every page that shows signals. */
export default function SignalModeBar() {
  const mode = useSignalMode()
  const { news } = useFeatures()
  const { pathname } = useLocation()
  const shown = SIGNAL_PAGES.some((p) => (p === '/' ? pathname === '/' : pathname.startsWith(p)))
  if (!shown) return null

  return (
    <div style={{ backgroundColor: '#e9eef3', borderBottom: `1px solid ${COLORS.border}` }}>
      <div style={{ maxWidth: '1200px', margin: '0 auto', padding: '8px 20px', display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '8px', fontSize: '14px' }}>
        <span style={{ color: COLORS.neutral }}>
          Technical signal<InfoTip term="steadySignal" />
        </span>
        <div role="radiogroup" aria-label="Technical signal" style={{ display: 'inline-flex', border: `1px solid #b0bec5`, borderRadius: '6px', overflow: 'hidden' }}>
          {CHOICES.map(({ mode: choice, label }) => (
            <button
              key={choice}
              type="button"
              role="radio"
              aria-checked={mode === choice}
              onClick={() => setSignalMode(choice)}
              style={{
                padding: '5px 12px',
                border: 'none',
                backgroundColor: mode === choice ? '#37474f' : 'white',
                color: mode === choice ? 'white' : '#37474f',
                cursor: 'pointer',
                fontSize: '13px',
                fontWeight: mode === choice ? 'bold' : 'normal',
              }}
            >
              {label}
            </button>
          ))}
        </div>
        <span style={{ color: COLORS.muted, fontSize: '12px' }}>
          {mode === 'steady' ? 'Changes a few times a year; did better in testing.' : 'Reacts to every day; changes often.'}{news ? ' The combined signal follows this choice.' : ''}
        </span>
      </div>
    </div>
  )
}
