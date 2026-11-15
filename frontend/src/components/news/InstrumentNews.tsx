import { useEffect, useState } from 'react'
import { newsApi } from '../../services/api/newsApi'
import { InstrumentMoves, NewsArticle, NewsSentimentSummary } from '../../services/types/marketData'
import { changeColor, COLORS, formatDate, formatNumber, formatPercent, SURFACE } from '../../services/format'
import { describeMood } from '../../services/plainLanguage'
import InfoTip from '../help/InfoTip'
import ArticleCard from './ArticleCard'
import SentimentChip from './SentimentChip'

interface InstrumentNewsProps {
  symbol: string
}

const sectionStyle = { padding: '20px', ...SURFACE, marginTop: '12px' }

export default function InstrumentNews({ symbol }: InstrumentNewsProps) {
  const [mood, setMood] = useState<NewsSentimentSummary>()
  const [moves, setMoves] = useState<InstrumentMoves>()
  const [articles, setArticles] = useState<NewsArticle[]>([])
  const [showBackground, setShowBackground] = useState(false)
  const [error, setError] = useState<string | null>(null)

  useEffect(() => {
    let cancelled = false
    setError(null)
    Promise.all([
      newsApi.getSentiment(7),
      newsApi.getMoves(symbol, 30, 5),
      newsApi.getNews({ symbol, days: 30, limit: 60 }),
    ])
      .then(([sentiment, movesData, news]) => {
        if (cancelled) return
        setMood(sentiment.find((s) => s.symbol === symbol))
        setMoves(movesData)
        setArticles(news)
      })
      .catch((err) => {
        console.error('Error loading news:', err)
        if (!cancelled) setError('Could not load news.')
      })
    return () => { cancelled = true }
  }, [symbol])

  const isDirect = (a: NewsArticle) => a.instruments.some((i) => i.symbol === symbol && i.isDirect)
  const visible = articles.filter((a) => showBackground || isDirect(a)).slice(0, 15)
  // Five stories first keeps the page short, especially on a phone
  const [showAllNews, setShowAllNews] = useState(false)
  const backgroundCount = articles.filter((a) => !isDirect(a)).length

  if (error) return <section style={sectionStyle}><span style={{ color: COLORS.down }}>{error}</span></section>

  return (
    <>
      <section style={sectionStyle}>
        <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '10px', marginBottom: '8px' }}>
          <h2 style={{ fontSize: '1.2rem' }}>
            News mood (last 7 days)<InfoTip term="newsMood" />
          </h2>
          {mood && mood.articles > 0 && (
            <SentimentChip label={mood.label} score={mood.score} confidence={mood.confidence} />
          )}
        </div>
        <p style={{ lineHeight: 1.5 }}>{describeMood(mood, symbol) ?? 'Loading...'}</p>
      </section>

      <section style={sectionStyle}>
        <h2 style={{ fontSize: '1.2rem', marginBottom: '4px' }}>
          Biggest moves and the news around them<InfoTip term="bigMoves" />
        </h2>
        <p style={{ color: COLORS.muted, fontSize: '13px', marginBottom: '8px' }}>
          The largest daily changes in the last 30 days (the period news is collected for).
          {moves?.typicalDailyMove != null && ` On a normal day ${symbol} moves about ${moves.typicalDailyMove.toFixed(2)}%.`}
          {moves?.newsAvailableSince && ` News is available from ${formatDate(moves.newsAvailableSince)}.`}
        </p>

        {moves?.moves.length === 0 && <p>No price data in this period.</p>}
        {moves?.moves.map((move) => (
          <details key={move.date} style={{ borderTop: `1px solid ${COLORS.border}`, padding: '10px 0' }}>
            <summary style={{ cursor: 'pointer', display: 'flex', flexWrap: 'wrap', gap: '12px', alignItems: 'baseline' }}>
              <strong>{formatDate(move.date)}</strong>
              <strong style={{ color: changeColor(move.change), fontSize: '16px' }}>{formatPercent(move.change)}</strong>
              {move.timesTypical != null && (
                <span style={{ color: COLORS.muted, fontSize: '13px' }}>{move.timesTypical.toFixed(1)}× a normal day</span>
              )}
              <span style={{ color: COLORS.muted, fontSize: '13px' }}>
                closed at {formatNumber(move.price)} · {move.articles.length === 0 ? 'no related news found' : `${move.articles.length} related ${move.articles.length === 1 ? 'story' : 'stories'}`}
              </span>
            </summary>
            <div style={{ paddingLeft: '16px' }}>
              {move.articles.length === 0 ? (
                <p style={{ color: COLORS.muted, fontSize: '13px', paddingTop: '6px' }}>
                  No stored news from {formatDate(move.newsFrom)} to {formatDate(move.newsTo)}. Moves can also come from things that don't make the headlines.
                </p>
              ) : (
                move.articles.map((a) => <ArticleCard key={a.id} article={a} symbol={symbol} />)
              )}
            </div>
          </details>
        ))}
      </section>

      <section style={sectionStyle}>
        <div style={{ display: 'flex', flexWrap: 'wrap', justifyContent: 'space-between', alignItems: 'center', gap: '8px' }}>
          <h2 style={{ fontSize: '1.2rem' }}>Latest news about {symbol}</h2>
          {backgroundCount > 0 && (
            <label style={{ fontSize: '13px', color: COLORS.muted, cursor: 'pointer' }}>
              <input type="checkbox" checked={showBackground} onChange={(e) => setShowBackground(e.target.checked)} />{' '}
              Include {backgroundCount} background {backgroundCount === 1 ? 'story' : 'stories'}
              <InfoTip term="background" />
            </label>
          )}
        </div>
        {visible.length === 0 ? (
          <p style={{ color: COLORS.muted, marginTop: '8px' }}>No news in the last 30 days.</p>
        ) : (
          <>
            {(showAllNews ? visible : visible.slice(0, 5)).map((a) => <ArticleCard key={a.id} article={a} symbol={symbol} />)}
            {visible.length > 5 && (
              <button
                type="button"
                onClick={() => setShowAllNews(!showAllNews)}
                style={{ display: 'block', width: '100%', marginTop: '8px', padding: '8px', border: `1px solid ${COLORS.border}`, borderRadius: '6px', backgroundColor: 'white', color: COLORS.link, cursor: 'pointer', fontSize: '14px' }}
              >
                {showAllNews ? 'Show fewer' : `Show ${visible.length - 5} more`}
              </button>
            )}
          </>
        )}
      </section>
    </>
  )
}
