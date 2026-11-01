import { useEffect, useRef, useState } from 'react'
import { glossary, GlossaryKey } from '../../services/glossary'

interface InfoTipProps {
  /** Glossary entry to show */
  term?: GlossaryKey
  /** Or custom text */
  title?: string
  text?: string
}

/**
 * A small ⓘ button that explains a term in plain language.
 * Opens on hover (mouse), tap (touch) or Enter/Space (keyboard); Escape or clicking elsewhere closes it.
 */
export default function InfoTip({ term, title, text }: InfoTipProps) {
  const [open, setOpen] = useState(false)
  const [alignRight, setAlignRight] = useState(false)
  const ref = useRef<HTMLSpanElement>(null)

  const entry = term ? glossary[term] : undefined
  const heading = title ?? entry?.title
  const body = text ?? entry?.what
  const howToRead = text ? undefined : entry?.howToRead

  useEffect(() => {
    if (!open) return
    // Open towards whichever side has room
    const rect = ref.current?.getBoundingClientRect()
    if (rect) setAlignRight(rect.left > window.innerWidth - 320)

    const close = (e: MouseEvent | KeyboardEvent) => {
      if (e instanceof KeyboardEvent ? e.key === 'Escape' : !ref.current?.contains(e.target as Node)) setOpen(false)
    }
    document.addEventListener('mousedown', close)
    document.addEventListener('keydown', close)
    return () => {
      document.removeEventListener('mousedown', close)
      document.removeEventListener('keydown', close)
    }
  }, [open])

  return (
    <span
      ref={ref}
      style={{ position: 'relative', display: 'inline-block', marginLeft: '4px', verticalAlign: 'middle' }}
      onMouseEnter={() => setOpen(true)}
      onMouseLeave={() => setOpen(false)}
    >
      <button
        type="button"
        aria-label={`What is ${heading}?`}
        aria-expanded={open}
        onClick={(e) => {
          e.stopPropagation()
          setOpen(!open)
        }}
        style={{
          width: '16px',
          height: '16px',
          padding: 0,
          borderRadius: '50%',
          border: '1px solid #9e9e9e',
          backgroundColor: 'white',
          color: '#616161',
          fontSize: '11px',
          fontWeight: 'bold',
          fontStyle: 'italic',
          lineHeight: '14px',
          cursor: 'help',
          fontFamily: 'Georgia, serif',
        }}
      >
        i
      </button>
      {open && (
        <span
          role="tooltip"
          style={{
            position: 'absolute',
            top: '22px',
            [alignRight ? 'right' : 'left']: '-8px',
            zIndex: 1000,
            width: '300px',
            maxWidth: 'calc(100vw - 32px)',
            padding: '12px 14px',
            backgroundColor: '#263238',
            color: 'white',
            borderRadius: '6px',
            boxShadow: '0 4px 12px rgba(0,0,0,0.25)',
            fontSize: '13px',
            fontWeight: 'normal',
            lineHeight: 1.45,
            textAlign: 'left',
            textTransform: 'none',
            whiteSpace: 'normal',
          }}
        >
          {heading && <strong style={{ display: 'block', marginBottom: '4px' }}>{heading}</strong>}
          {body}
          {howToRead && <span style={{ display: 'block', marginTop: '6px', color: '#cfd8dc' }}>{howToRead}</span>}
        </span>
      )}
    </span>
  )
}
