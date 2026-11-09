import { useEffect } from 'react'
import { useLocation } from 'react-router-dom'

const TITLES: Record<string, string> = {
  '/': 'Market Overview',
  '/holdings': 'My Holdings',
  '/news': 'Market News',
  '/portfolio': 'Portfolio Mix',
  '/comparison': 'Compare',
  '/track-record': 'Track Record',
  '/guide': 'Guide',
  '/settings': 'Settings',
}

/** Names the browser tab after the page, so tabs, history and screen readers say where you are. */
export default function PageTitle() {
  const { pathname } = useLocation()

  useEffect(() => {
    const instrument = pathname.match(/^\/instrument\/([^/]+)/)?.[1]
    const page = instrument ? decodeURIComponent(instrument) : TITLES[pathname] ?? 'Page not found'
    document.title = `${page} · Market Analytics`
  }, [pathname])

  return null
}
