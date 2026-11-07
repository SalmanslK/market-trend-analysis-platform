import { ReactNode, useEffect, useRef, useState } from 'react'

interface TipProps {
  /** Explanation; line breaks are kept */
  text?: string
  children: ReactNode
}

/**
 * Shows an explanation for whatever it wraps: on hover with a mouse, on tap on a touchscreen.
 * Replaces the browser's title attribute, which phones never show. A tap only opens the tip, so a badge
 * inside a link or clickable row doesn't navigate away on touch; a mouse click still goes through.
 */
export default function Tip({ text, children }: TipProps) {
  const [open, setOpen] = useState(false)
  const [alignRight, setAlignRight] = useState(false)
  const ref = useRef<HTMLSpanElement>(null)
  const touched = useRef(false)

  useEffect(() => {
    if (!open) return
    const rect = ref.current?.getBoundingClientRect()
    if (rect) setAlignRight(rect.left > window.innerWidth - 300)

    const close = (e: Event) => {
      if (e instanceof KeyboardEvent ? e.key === 'Escape' : !ref.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('pointerdown', close)
    document.addEventListener('keydown', close)
    return () => {
      document.removeEventListener('pointerdown', close)
      document.removeEventListener('keydown', close)
    }
  }, [open])

  if (!text) return <>{children}</>

  return (
    <span
      ref={ref}
      style={{ position: 'relative', display: 'inline-block' }}
      onPointerDown={(e) => { touched.current = e.pointerType !== 'mouse' }}
      onMouseEnter={() => { if (!touched.current) setOpen(true) }}
      onMouseLeave={() => { if (!touched.current) setOpen(false) }}
      onClick={(e) => {
        if (!touched.current) return
        e.preventDefault()
        e.stopPropagation()
        setOpen(!open)
      }}
    >
      {children}
      {open && (
        <span
          role="tooltip"
          style={{
            position: 'absolute',
            top: 'calc(100% + 6px)',
            [alignRight ? 'right' : 'left']: 0,
            zIndex: 1000,
            width: 'max-content',
            maxWidth: 'min(300px, calc(100vw - 32px))',
            padding: '10px 12px',
            backgroundColor: '#263238',
            color: 'white',
            borderRadius: '6px',
            boxShadow: '0 4px 12px rgba(0,0,0,0.25)',
            fontSize: '13px',
            fontWeight: 'normal',
            lineHeight: 1.45,
            textAlign: 'left',
            whiteSpace: 'pre-line',
          }}
        >
          {text}
        </span>
      )}
    </span>
  )
}
