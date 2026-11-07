import { useEffect, useState } from 'react'
import { Link, useLocation } from 'react-router-dom'
import { useIsNarrow } from '../../services/useIsNarrow'

const LINKS = [
  { path: '/', label: 'Overview' },
  { path: '/news', label: 'News' },
  { path: '/comparison', label: 'Comparison' },
  { path: '/track-record', label: 'Track Record' },
  { path: '/portfolio', label: 'Portfolio' },
  { path: '/holdings', label: 'My Holdings' },
  { path: '/guide', label: 'Guide' },
  { path: '/settings', label: 'Settings' },
]

export default function Navigation() {
  const location = useLocation()
  // On phones the nine links would take four rows, so they fold behind a menu button
  const narrow = useIsNarrow()
  const [menuOpen, setMenuOpen] = useState(false)
  useEffect(() => setMenuOpen(false), [location.pathname])

  const isActive = (path: string) => {
    if (path === '/') {
      // Instrument pages belong to the overview
      return location.pathname === '/' || location.pathname.startsWith('/instrument')
    }
    return location.pathname.startsWith(path)
  }

  return (
    <nav style={{ backgroundColor: '#333', padding: '0.75rem 0' }}>
      <div style={{
        maxWidth: '1200px',
        margin: '0 auto',
        padding: '0 20px',
        display: 'flex',
        flexWrap: 'wrap',
        gap: '0.5rem 1.5rem',
        alignItems: 'center',
      }}>
        <Link
          to="/"
          style={{
            color: '#fff',
            textDecoration: 'none',
            fontSize: '1.5rem',
            fontWeight: 'bold',
          }}
        >
          Market Analytics
        </Link>

        {narrow && (
          <button
            type="button"
            aria-expanded={menuOpen}
            aria-controls="main-menu"
            onClick={() => setMenuOpen(!menuOpen)}
            style={{ marginLeft: 'auto', padding: '0.4rem 0.8rem', backgroundColor: 'rgba(255,255,255,0.1)', color: '#fff', border: '1px solid rgba(255,255,255,0.3)', borderRadius: '4px', fontSize: '15px', cursor: 'pointer' }}
          >
            {menuOpen ? '✕ Close' : `☰ ${LINKS.find(({ path }) => isActive(path))?.label ?? 'Menu'}`}
          </button>
        )}

        {(!narrow || menuOpen) && (
        <div
          id="main-menu"
          style={narrow
            ? { display: 'flex', flexDirection: 'column', width: '100%', gap: '0.25rem' }
            : { display: 'flex', flexWrap: 'wrap', gap: '0.25rem', marginLeft: 'auto' }}
        >
          {LINKS.map(({ path, label }) => (
            <Link
              key={path}
              to={path}
              style={{
                color: isActive(path) ? '#4CAF50' : '#fff',
                textDecoration: 'none',
                padding: '0.5rem 0.9rem',
                borderRadius: '4px',
                backgroundColor: isActive(path) ? 'rgba(255,255,255,0.1)' : 'transparent',
              }}
            >
              {label}
            </Link>
          ))}
        </div>
        )}
      </div>
    </nav>
  )
}
