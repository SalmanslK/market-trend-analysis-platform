import { Link } from 'react-router-dom'
import { COLORS, SURFACE } from '../../services/format'

// The things people come to do, each one click away, so nobody has to search the menu
const STEPS = [
  { to: '/holdings', icon: '👛', title: 'Track what I own', text: 'Enter your funds or gold to see their value, payouts and signals.' },
  { to: '/portfolio', icon: '⚖️', title: 'Try a mix', text: 'See how splitting money between stocks, gold and cash would have done.' },
  { to: '/comparison', icon: '📈', title: 'Compare investments', text: 'Put two or more on one chart to see which did better.' },
  { to: '/guide', icon: '💡', title: 'Learn the basics', text: 'What every signal, number and chart means, in plain words.' },
]

/** "What would you like to do?": the main tasks as large, clear links. */
export default function NextSteps() {
  return (
    <section aria-labelledby="next-title" style={{ marginBottom: '28px' }}>
      <h2 id="next-title" style={{ fontSize: '1.2rem', marginBottom: '12px' }}>What would you like to do?</h2>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(220px, 1fr))', gap: '14px' }}>
        {STEPS.map((step) => (
          <Link
            key={step.to}
            to={step.to}
            className="action-tile"
            style={{ ...SURFACE, display: 'flex', gap: '12px', padding: '16px', textDecoration: 'none', color: 'inherit' }}
          >
            <span aria-hidden="true" style={{ fontSize: '24px', lineHeight: 1 }}>{step.icon}</span>
            <span>
              <span style={{ display: 'block', fontWeight: 700, color: COLORS.link, marginBottom: '2px' }}>{step.title}</span>
              <span style={{ fontSize: '14px', color: COLORS.muted }}>{step.text}</span>
            </span>
          </Link>
        ))}
      </div>
    </section>
  )
}
