import { ReactNode } from 'react'
import { ANSWER_SURFACE, COLORS } from '../../services/format'

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
    <section aria-label="The short answer" style={{ ...ANSWER_SURFACE, padding: '20px 22px', marginBottom: '20px' }}>
      <div style={{ fontSize: '15px', fontWeight: 700, color: COLORS.muted }}>{question}</div>
      <p style={{ fontSize: answer.length > 140 ? '18px' : '21px', fontWeight: 700, color: COLORS.ink, lineHeight: 1.4, margin: '6px 0 14px' }}>{answer}</p>
      {facts.length > 0 && (
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(180px, 100%), 1fr))', gap: '10px', marginBottom: meaning ? '14px' : 0 }}>
          {facts.map((fact) => (
            <div
              key={fact.label}
              style={{ padding: '10px 12px', borderRadius: '10px', backgroundColor: fact.emphasis ? '#e6efe9' : '#eef2ee' }}
            >
              <div style={{ fontSize: '12px', color: COLORS.muted, fontWeight: 600 }}>{fact.label}</div>
              <div style={{ fontSize: '20px', fontWeight: 700, color: '#0e3b2e', fontVariantNumeric: 'tabular-nums' }}>{fact.value}</div>
              {fact.note && <div style={{ fontSize: '13px', color: COLORS.muted }}>{fact.note}</div>}
            </div>
          ))}
        </div>
      )}
      {meaning && (
        <p style={{ fontSize: '15px', color: '#2f3d37', borderLeft: '3px solid #81c784', paddingLeft: '10px' }}>
          <strong style={{ color: '#1b5e20' }}>What this means for you: </strong>{meaning}
        </p>
      )}
      {children}
    </section>
  )
}
