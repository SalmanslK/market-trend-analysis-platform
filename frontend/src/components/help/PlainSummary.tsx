import { ReactNode, useState } from 'react'
import { COLORS } from '../../services/format'

interface PlainSummaryProps {
  title?: string
  headline?: ReactNode
  points: ReactNode[]
  footer?: ReactNode
  /** Show only this many points at first, with a button for the rest */
  maxPoints?: number
}

/** "In plain words" card: the key message of a page in a few sentences, for readers without a finance background. */
export default function PlainSummary({ title = 'In plain words', headline, points, footer, maxPoints }: PlainSummaryProps) {
  const [expanded, setExpanded] = useState(false)
  const folded = maxPoints != null && points.length > maxPoints && !expanded
  const shown = folded ? points.slice(0, maxPoints) : points

  return (
    <section
      style={{
        padding: '16px 20px',
        backgroundColor: '#e8f1fb',
        borderLeft: '4px solid #1565c0',
        borderRadius: '6px',
        marginBottom: '16px',
        lineHeight: 1.5,
      }}
    >
      <div style={{ fontSize: '12px', fontWeight: 'bold', textTransform: 'uppercase', letterSpacing: '0.5px', color: '#0d47a1', marginBottom: '6px' }}>
        {title}
      </div>
      {headline && <div style={{ fontSize: '17px', fontWeight: 'bold', marginBottom: '6px' }}>{headline}</div>}
      <ul style={{ margin: 0, paddingLeft: '20px' }}>
        {shown.map((point, i) => (
          <li key={i} style={{ marginBottom: '3px' }}>{point}</li>
        ))}
      </ul>
      {maxPoints != null && points.length > maxPoints && (
        <button
          type="button"
          aria-expanded={!folded}
          onClick={() => setExpanded(!expanded)}
          style={{ marginTop: '4px', border: 'none', background: 'none', padding: 0, color: '#0d47a1', fontSize: '14px', textDecoration: 'underline' }}
        >
          {folded ? `Show all ${points.length} points` : 'Show fewer'}
        </button>
      )}
      {footer && <div style={{ marginTop: '8px', fontSize: '12px', color: COLORS.muted }}>{footer}</div>}
    </section>
  )
}
