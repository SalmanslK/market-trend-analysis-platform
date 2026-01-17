import { Link, useLocation } from 'react-router-dom'

export default function Navigation() {
  const location = useLocation()

  const isActive = (path: string) => {
    if (path === '/') {
      return location.pathname === '/'
    }
    return location.pathname.startsWith(path)
  }

  return (
    <nav style={{
      backgroundColor: '#333',
      padding: '1rem',
      marginBottom: '2rem',
    }}>
      <div style={{
        maxWidth: '1200px',
        margin: '0 auto',
        display: 'flex',
        gap: '2rem',
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
        
        <div style={{ display: 'flex', gap: '1.5rem', marginLeft: 'auto' }}>
          <Link
            to="/"
            style={{
              color: isActive('/') ? '#4CAF50' : '#fff',
              textDecoration: 'none',
              padding: '0.5rem 1rem',
              borderRadius: '4px',
              backgroundColor: isActive('/') ? 'rgba(255,255,255,0.1)' : 'transparent',
            }}
          >
            Dashboard
          </Link>
          
          <Link
            to="/comparison"
            style={{
              color: isActive('/comparison') ? '#4CAF50' : '#fff',
              textDecoration: 'none',
              padding: '0.5rem 1rem',
              borderRadius: '4px',
              backgroundColor: isActive('/comparison') ? 'rgba(255,255,255,0.1)' : 'transparent',
            }}
          >
            Comparison
          </Link>
          
          <Link
            to="/settings"
            style={{
              color: isActive('/settings') ? '#4CAF50' : '#fff',
              textDecoration: 'none',
              padding: '0.5rem 1rem',
              borderRadius: '4px',
              backgroundColor: isActive('/settings') ? 'rgba(255,255,255,0.1)' : 'transparent',
            }}
          >
            Settings
          </Link>
        </div>
      </div>
    </nav>
  )
}