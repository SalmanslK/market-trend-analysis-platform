import { Link } from 'react-router-dom'
import { COLORS } from '../services/format'

/** Shown for an address the app doesn't have, instead of a blank page. */
export default function NotFound() {
  return (
    <div style={{ padding: '40px 20px' }}>
      <h1>Page not found</h1>
      <p style={{ color: COLORS.muted, margin: '8px 0 16px' }}>
        This address doesn't match a page in the app. It may have been mistyped or moved.
      </p>
      <Link to="/" style={{ color: COLORS.link }}>Go to the Market Overview</Link>
    </div>
  )
}
