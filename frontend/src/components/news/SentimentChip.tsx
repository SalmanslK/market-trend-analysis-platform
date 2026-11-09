import { SentimentLabel } from '../../services/types/marketData'
import Tip from '../help/Tip'

const styles: Record<SentimentLabel, { icon: string; background: string; color: string }> = {
  Positive: { icon: '▲', background: '#e8f5e9', color: '#1b5e20' },
  Neutral: { icon: '●', background: '#f5f5f5', color: '#616161' },
  Negative: { icon: '▼', background: '#ffebee', color: '#b71c1c' },
  Background: { icon: '○', background: '#f5f5f5', color: '#616161' },
}

interface SentimentChipProps {
  label: SentimentLabel
  score?: number
  confidence?: number
  /** Words that drove the reading, shown on hover or tap */
  positiveWords?: string[]
  negativeWords?: string[]
  compact?: boolean
}

/** Positive / Neutral / Negative chip with score (-100 to +100) and confidence. */
export default function SentimentChip({ label, score, confidence, positiveWords = [], negativeWords = [], compact = false }: SentimentChipProps) {
  const style = styles[label]
  const tooltip = label === 'Background'
    ? 'Background news: affects this instrument indirectly, so its tone is not scored for it'
    : [
        `Score ${score != null && score > 0 ? '+' : ''}${score ?? 0} (from -100 very negative to +100 very positive)`,
        confidence != null ? `Confidence ${confidence}%` : '',
        positiveWords.length ? `Positive words: ${positiveWords.join(', ')}` : '',
        negativeWords.length ? `Negative words: ${negativeWords.join(', ')}` : '',
        positiveWords.length + negativeWords.length === 0 && label === 'Neutral' ? 'No clearly positive or negative words found' : '',
      ].filter(Boolean).join('\n')

  return (
    <Tip text={tooltip}>
    <span
      style={{
        display: 'inline-flex',
        alignItems: 'center',
        gap: '4px',
        padding: compact ? '1px 8px' : '3px 10px',
        borderRadius: '10px',
        fontSize: compact ? '12px' : '13px',
        fontWeight: 'bold',
        whiteSpace: 'nowrap',
        backgroundColor: style.background,
        color: style.color,
        cursor: 'help',
      }}
    >
      {style.icon} {label}
      {label !== 'Background' && score != null && (
        <span style={{ fontWeight: 'normal' }}>{score > 0 ? '+' : ''}{score}</span>
      )}
      {label !== 'Background' && confidence != null && !compact && (
        <span style={{ fontWeight: 'normal' }}>· {confidence}% sure</span>
      )}
    </span>
    </Tip>
  )
}
