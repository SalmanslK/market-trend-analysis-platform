import { useState } from 'react'
import { COLORS } from '../../services/format'

const DISMISSED_KEY = 'welcome.dismissed'

function readDismissed(): boolean {
  try {
    return localStorage.getItem(DISMISSED_KEY) === 'yes'
  } catch {
    return false
  }
}

/**
 * A first-visit orientation: what the app is and how to use it in three steps. Closed for good with one click,
 * so regular users never see it again.
 */
export default function WelcomeBanner() {
  const [dismissed, setDismissed] = useState(readDismissed)
  if (dismissed) return null

  const close = () => {
    setDismissed(true)
    try {
      localStorage.setItem(DISMISSED_KEY, 'yes')
    } catch {
      // Only a convenience: it shows again next visit
    }
  }

  return (
    <section
      aria-labelledby="welcome-title"
      style={{ position: 'relative', marginBottom: '24px', padding: '20px 22px', borderRadius: '12px', backgroundColor: '#0e3b2e', color: 'white' }}
    >
      <button
        type="button"
        onClick={close}
        aria-label="Close the welcome message"
        style={{ position: 'absolute', top: '10px', right: '12px', border: 'none', background: 'none', color: '#d8ddd8', fontSize: '20px', lineHeight: 1 }}
      >
        ✕
      </button>
      <h2 id="welcome-title" style={{ color: 'white', fontSize: '1.25rem', marginBottom: '6px' }}>Welcome! Here's how to use this app</h2>
      <p style={{ color: '#d8ddd8', marginBottom: '12px', maxWidth: '760px' }}>
        It follows Pakistan stocks, gold, the dollar rate and Meezan funds, and explains in plain words what their prices have been doing.
      </p>
      <ol style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '10px 20px', listStyle: 'none', padding: 0 }}>
        {[
          ['1', 'Read "Today at a glance"', 'for the big picture in one sentence per market.'],
          ['2', 'Open an instrument', 'for its chart, its signals and what they mean.'],
          ['3', 'Add what you own', 'under My Holdings to follow your own money.'],
        ].map(([n, title, text]) => (
          <li key={n} style={{ display: 'flex', gap: '10px' }}>
            <span aria-hidden="true" style={{ flex: '0 0 26px', height: '26px', borderRadius: '50%', backgroundColor: '#81c784', color: '#0e3b2e', fontWeight: 700, textAlign: 'center', lineHeight: '26px' }}>{n}</span>
            <span><strong>{title}</strong> <span style={{ color: '#d8ddd8' }}>{text}</span></span>
          </li>
        ))}
      </ol>
      <p style={{ marginTop: '12px', fontSize: '13px', color: '#bcccdc' }}>
        Signals describe past prices; they aren't predictions or advice. Tap any <span style={{ color: COLORS.border }}>ⓘ</span> to learn what a number means.
      </p>
    </section>
  )
}
