import { useEffect, useState } from 'react'

// Phones and narrow windows
const NARROW = '(max-width: 700px)'

/** True on narrow screens; updates when the window is resized or the phone rotated. */
export function useIsNarrow(): boolean {
  const query = typeof window !== 'undefined' && window.matchMedia ? window.matchMedia(NARROW) : null
  const [narrow, setNarrow] = useState(query?.matches ?? false)

  useEffect(() => {
    if (!query) return
    const update = () => setNarrow(query.matches)
    query.addEventListener('change', update)
    return () => query.removeEventListener('change', update)
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [])

  return narrow
}
