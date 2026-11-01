import { ReactNode } from 'react'

interface PlainSummaryProps {
  title?: string
  headline?: ReactNode
  points: ReactNode[]
  footer?: ReactNode
}

/** "In plain words" card: the key message of a page in a few sentences, for readers without a finance background. */
export default function PlainSummary({ title = 'In plain words', headline, points, footer }: PlainSummaryProps) {
  return (
    <section
      style={{
        padding: '16px 20px',
        backgroundColor: '#e8f1fb',
        borderLeft: '4px solid #1976d2',
        borderRadius: '6px',
        marginBottom: '16px',
        lineHeight: 1.5,
      }}
    >
      <div style={{ fontSize: '12px', fontWeight: 'bold', textTransform: 'uppercase', letterSpacing: '0.5px', color: '#1565c0', marginBottom: '6px' }}>
        {title}
      </div>
      {headline && <div style={{ fontSize: '17px', fontWeight: 'bold', marginBottom: '6px' }}>{headline}</div>}
      <ul style={{ margin: 0, paddingLeft: '20px' }}>
        {points.map((point, i) => (
          <li key={i} style={{ marginBottom: '3px' }}>{point}</li>
        ))}
      </ul>
      {footer && <div style={{ marginTop: '8px', fontSize: '12px', color: '#546e7a' }}>{footer}</div>}
    </section>
  )
}
