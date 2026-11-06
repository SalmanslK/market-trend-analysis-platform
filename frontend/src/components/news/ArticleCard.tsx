import { Link } from 'react-router-dom'
import { NewsArticle } from '../../services/types/marketData'
import { changeColor, COLORS, formatPercent } from '../../services/format'
import SentimentChip from './SentimentChip'

interface ArticleCardProps {
  article: NewsArticle
  /** When shown on an instrument's page, focus on that instrument */
  symbol?: string
}

export function timeAgo(value: string): string {
  const minutes = Math.round((Date.now() - new Date(value).getTime()) / 60000)
  if (minutes < 60) return `${Math.max(minutes, 1)} min ago`
  const hours = Math.round(minutes / 60)
  if (hours < 24) return `${hours} h ago`
  const days = Math.round(hours / 24)
  return days < 30 ? `${days} d ago` : new Date(value).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })
}

export default function ArticleCard({ article, symbol }: ArticleCardProps) {
  const focus = symbol ? article.instruments.find((i) => i.symbol === symbol) : undefined
  const others = article.instruments.filter((i) => i.symbol !== symbol)

  return (
    <article style={{ padding: '12px 0', borderTop: `1px solid ${COLORS.border}` }}>
      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '8px', marginBottom: '4px' }}>
        {focus ? (
          <SentimentChip
            label={focus.sentimentLabel}
            score={focus.sentimentScore}
            confidence={focus.isDirect ? article.sentiment.confidence : undefined}
            positiveWords={article.sentiment.positiveWords}
            negativeWords={article.sentiment.negativeWords}
          />
        ) : (
          <SentimentChip
            label={article.sentiment.label}
            score={article.sentiment.score}
            confidence={article.sentiment.confidence}
            positiveWords={article.sentiment.positiveWords}
            negativeWords={article.sentiment.negativeWords}
          />
        )}
        <span style={{ fontSize: '12px', color: COLORS.muted }}>
          {article.source} · {timeAgo(article.publishedAt)}
        </span>
      </div>

      <a href={article.url} target="_blank" rel="noopener noreferrer" style={{ color: '#212121', fontWeight: 'bold', textDecoration: 'none', lineHeight: 1.4 }}>
        {article.title} <span style={{ color: COLORS.muted, fontWeight: 'normal' }}>↗</span>
      </a>
      {article.summary && <p style={{ margin: '4px 0 0', fontSize: '13px', color: '#424242', lineHeight: 1.45 }}>{article.summary}</p>}

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '6px', marginTop: '8px', fontSize: '12px' }}>
        {focus?.change != null && (
          <span style={{ color: COLORS.muted }}>
            {symbol} that day: <strong style={{ color: changeColor(focus.change) }}>{formatPercent(focus.change)}</strong>
          </span>
        )}
        {article.topics.map((topic) => (
          <span
            key={topic.key}
            title={topic.whyItMatters}
            style={{ padding: '1px 8px', borderRadius: '10px', backgroundColor: '#eceff1', color: '#455a64', cursor: 'help' }}
          >
            {topic.label}
          </span>
        ))}
        {others.map((i) => (
          <Link
            key={i.symbol}
            to={`/instrument/${i.symbol}`}
            title={i.isDirect ? `${i.symbol}: ${i.sentimentLabel} for its price` : `${i.symbol}: background news`}
            style={{ padding: '1px 8px', borderRadius: '10px', border: `1px solid ${COLORS.border}`, color: COLORS.link, textDecoration: 'none' }}
          >
            {i.symbol}
            {i.change != null && <span style={{ color: changeColor(i.change), marginLeft: '4px' }}>{formatPercent(i.change, 1)}</span>}
          </Link>
        ))}
      </div>
    </article>
  )
}
