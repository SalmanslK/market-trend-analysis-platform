import { ReactNode } from 'react'
import { COLORS, SURFACE } from '../../services/format'

export interface AnswerFact {
  label: string
  value: string
  note?: string
  /** Highlights the fact the answer is about */
  emphasis?: boolean
}

interface AnswerCardProps {
  /** The question the page answers, e.g. "Would following the signals have paid off?" */
  question: string
  /** The answer in one or two plain sentences */
  answer: ReactNode
  facts?: AnswerFact[]
  /** What the answer means for the reader: one practical line */
  meaning?: ReactNode
  children?: ReactNode
}

/**
 * The first thing on a page: the page's question, its answer in plain words, the few numbers behind it and what it
 * means for the reader. Everything else on the page is the detail behind this card.
 */
export default function AnswerCard({ question, answer, facts = [], meaning, children }: AnswerCardProps) {
  return (
    <section aria-label="The short answer" style={{ ...SURFACE, padding: '20px 22px', marginBottom: '20px', borderTop: '4px solid #1565c0' }}>
      <div style={{ fontSize: '13px', fontWeight: 700, color: '#1565c0', textTransform: 'uppercase', letterSpacing: '0.4px' }}>{question}</div>
      <p style={{ fontSize: '19px', fontWeight: 600, color: '#102a43', lineHeight: 1.4, margin: '8px 0 14px' }}>{answer}</p>
      {facts.length > 0 && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(180px, 100%), 1fr))', gap: '10px', marginBottom: meaning ? '14px' : 0 }}>
          {facts.map((fact) => (
            <div
              key={fact.label}
              style={{ padding: '10px 12px', borderRadius: '10px', backgroundColor: fact.emphasis ? '#e3f2fd' : '#f0f4f8' }}
            >
              <div style={{ fontSize: '12px', color: COLORS.muted, fontWeight: 600 }}>{fact.label}</div>
              <div style={{ fontSize: '20px', fontWeight: 700, color: '#102a43', fontVariantNumeric: 'tabular-nums' }}>{fact.value}</div>
              {fact.note && <div style={{ fontSize: '13px', color: COLORS.muted }}>{fact.note}</div>}
            </div>
          ))}
        </div>
      )}
      {meaning && (
        <p style={{ fontSize: '15px', color: '#334e68', borderLeft: '3px solid #81c784', paddingLeft: '10px' }}>
          <strong style={{ color: '#1b5e20' }}>What this means for you: </strong>{meaning}
        </p>
      )}
      {children}
    </section>
  )
}
