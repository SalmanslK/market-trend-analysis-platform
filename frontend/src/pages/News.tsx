import { useEffect, useMemo, useState } from 'react'
import { Link, useSearchParams } from 'react-router-dom'
import { newsApi } from '../services/api/newsApi'
import { instrumentsApi } from '../services/api/instrumentsApi'
import { groupOfType, INSTRUMENT_GROUPS, InstrumentGroup } from '../services/instrumentGroups'
import { GroupMood, groupMood } from '../services/groupMood'
import axios from 'axios'
import { NewsArticle, NewsSentimentSummary, NewsTopic, SentimentLabel } from '../services/types/marketData'
import { COLORS, SURFACE } from '../services/format'
import ArticleCard from '../components/news/ArticleCard'
import SentimentChip from '../components/news/SentimentChip'
import InfoTip from '../components/help/InfoTip'

const PAGE_SIZE = 30
import PlainSummary from '../components/help/PlainSummary'

const selectStyle = { padding: '8px', fontSize: '15px' }

export default function News() {
  // Filters live in the URL so a filtered view can be bookmarked or shared
  const [params, setParams] = useSearchParams()
  const symbol = params.get('symbol') ?? ''
  const topic = params.get('topic') ?? ''
  const mood = (params.get('mood') ?? '') as SentimentLabel | ''
  const group = (params.get('group') ?? '') as InstrumentGroup | ''

  const [articles, setArticles] = useState<NewsArticle[]>([])
  const [topics, setTopics] = useState<NewsTopic[]>([])
  const [moods, setMoods] = useState<NewsSentimentSummary[]>([])
  // Which group each instrument belongs to, for the Markets / Funds / Shariah stocks tabs
  const [groups, setGroups] = useState<Record<string, InstrumentGroup>>({})
  // Each tracked company's industry, from the stock rankings
  const [industries, setIndustries] = useState<Record<string, string>>({})
  const [loading, setLoading] = useState(true)
  const [showBackground, setShowBackground] = useState(false)
  const [error, setError] = useState<string | null>(null)
  // Stories are shown a page at a time; hundreds of cards make the page very long, especially on a phone
  const [shown, setShown] = useState(PAGE_SIZE)

  useEffect(() => {
    newsApi.getTopics().then(setTopics).catch((err) => console.error('Error loading topics:', err))
    newsApi.getSentiment(7).then(setMoods).catch((err) => console.error('Error loading sentiment:', err))
    instrumentsApi.getAll()
      .then((all) => setGroups(Object.fromEntries(all.map((i) => [i.symbol, groupOfType(i.type)]))))
      .catch((err) => console.error('Error loading instruments:', err))
    axios.get<{ final: { symbol: string; sector: string }[] }>('/data/stock-rankings.json')
      .then((r) => setIndustries(Object.fromEntries(r.data.final.map((f) => [f.symbol, f.sector]))))
      .catch((err) => console.error('Error loading industries:', err))
  }, [])

  useEffect(() => {
    let cancelled = false
    setLoading(true)
    setError(null)
    newsApi.getNews({ symbol: symbol || undefined, topic: topic || undefined, days: 30, limit: 300 })
      .then((data) => { if (!cancelled) setArticles(data) })
      .catch((err) => {
        console.error('Error loading news:', err)
        if (!cancelled) setError('Could not load news. Check that the API is running.')
      })
      .finally(() => { if (!cancelled) setLoading(false) })
    return () => { cancelled = true }
  }, [symbol, topic])

  const setFilter = (key: string, value: string) => {
    const next = new URLSearchParams(params)
    if (value) next.set(key, value)
    else next.delete(key)
    setParams(next, { replace: true })
  }

  // With an instrument selected, mood is judged for that instrument (USD/PKR flips, background isn't scored)
  const articleMood = (a: NewsArticle): SentimentLabel =>
    symbol ? a.instruments.find((i) => i.symbol === symbol)?.sentimentLabel ?? a.sentiment.label : a.sentiment.label

  const inGroup = (s: string) => !group || groups[s] === group
  // A tab shows instruments of that group and stories that directly concern one of them
  const groupMoods = moods.filter((m) => inGroup(m.symbol))

  // Combined moods: the whole picture on "All", finer parts inside a tab (Pakistan and global markets, each industry)
  const pick = (test: (symbol: string) => boolean) => moods.filter((m) => test(m.symbol))
  const isMarket = (sym: string) => groups[sym] === 'markets'
  // USD/PKR gets its own card: news that is good for it (a dearer dollar) is bad for Pakistan
  const marketParts: [string, (sym: string) => boolean][] = [
    ['Markets: Pakistan', (sym) => isMarket(sym) && sym !== 'GOLDINT' && sym !== 'USDPKR'],
    ['The dollar (USD/PKR)', (sym) => sym === 'USDPKR'],
    ['Markets: Global', (sym) => sym === 'GOLDINT'],
  ]
  const combined: (GroupMood | null)[] =
    group === 'markets' ? marketParts.map(([title, test]) => groupMood(title, pick(test)))
      : group === 'funds' ? [groupMood('Meezan funds', pick((sym) => groups[sym] === 'funds'))]
        : group === 'shares'
          ? [...new Set(Object.values(industries))].sort().map((industry) => groupMood(industry, pick((sym) => industries[sym] === industry)))
          : [
            ...marketParts.map(([title, test]) => groupMood(title, pick(test))),
            groupMood('Funds', pick((sym) => groups[sym] === 'funds')),
            groupMood('Shariah stocks', pick((sym) => groups[sym] === 'shares')),
          ]

  const visible = useMemo(
    () => articles
      .filter((a) => !group || a.instruments.some((i) => groups[i.symbol] === group && i.isDirect))
      .filter((a) => !mood || articleMood(a) === mood)
      // For one instrument, wider economic stories are hidden unless asked for
      .filter((a) => !symbol || showBackground || mood === 'Background' || articleMood(a) !== 'Background'),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [articles, mood, symbol, showBackground, group, groups],
  )
  useEffect(() => setShown(PAGE_SIZE), [articles, mood, symbol, showBackground, group])
  const backgroundCount = symbol ? articles.filter((a) => articleMood(a) === 'Background').length : 0

  const counts = {
    Positive: articles.filter((a) => articleMood(a) === 'Positive').length,
    Negative: articles.filter((a) => articleMood(a) === 'Negative').length,
  }

  return (
    <div style={{ padding: '20px' }}>
      <h1>Market News</h1>
      <p style={{ color: COLORS.muted, margin: '6px 0 16px' }}>
        Business news from Dawn, Business Recorder, Express Tribune and Google News, linked to the instruments it affects.
        Updated every hour.
      </p>

      <PlainSummary
        title="How to use this page"
        points={[
          'Each story is marked ▲ Positive, ● Neutral or ▼ Negative from its wording, with a score from -100 to +100 and how sure the reading is. Hover or tap the label to see which words decided it.',
          'Grey tags show the topic (hover or tap for why it matters); blue tags are the instruments it affects, with their price change that day.',
          'Pick an instrument to see only its news, judged from its point of view. Open a story to read it in full on the publisher\'s site.',
        ]}
      />

      <div role="tablist" aria-label="Instrument groups" style={{ display: 'flex', flexWrap: 'wrap', gap: '6px', marginBottom: '14px' }}>
        {[{ key: '' as const, title: 'All', note: 'Every story' }, ...INSTRUMENT_GROUPS].map((g) => {
          const selected = group === g.key
          return (
            <button
              key={g.key || 'all'}
              type="button"
              role="tab"
              aria-selected={selected}
              title={g.note}
              onClick={() => {
                // Switching group clears an instrument from another group
                const next = new URLSearchParams(params)
                if (g.key) next.set('group', g.key)
                else next.delete('group')
                if (symbol && g.key && groups[symbol] !== g.key) next.delete('symbol')
                setParams(next, { replace: true })
              }}
              style={{ padding: '8px 14px', borderRadius: '8px', fontSize: '15px', fontWeight: 700, cursor: 'pointer', border: `1px solid ${selected ? COLORS.ink : COLORS.border}`, backgroundColor: selected ? COLORS.ink : 'white', color: selected ? 'white' : COLORS.ink }}
            >
              {g.title}
            </button>
          )
        })}
      </div>

      {combined.some((c) => c) && (<>
        <h2 style={{ fontSize: '1.1rem', marginBottom: '8px' }}>
          {group === 'shares' ? 'Combined mood by industry' : 'Combined mood'} (last 7 days)<InfoTip term="newsMood" />
        </h2>
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(200px, 1fr))', gap: '8px', marginBottom: '20px' }}>
          {combined.filter((c): c is GroupMood => c !== null).map((c) => (
            <div key={c.title} style={{ ...SURFACE, padding: '12px 14px' }}>
              <div style={{ fontWeight: 'bold', marginBottom: '6px' }}>{c.title}</div>
              <SentimentChip label={c.label} score={c.score} confidence={c.confidence} compact />
              <div style={{ fontSize: '12px', color: COLORS.muted, marginTop: '6px' }}>
                {c.symbols.length > 1 ? `Average of ${c.symbols.join(', ')}` : `${c.articles} ${c.articles === 1 ? 'story' : 'stories'} about ${c.symbols[0]}`}
              </div>
            </div>
          ))}
        </div>
      </>)}

      <h2 style={{ fontSize: '1.1rem', marginBottom: '8px' }}>
        News mood by instrument (last 7 days)<InfoTip term="newsMood" />
      </h2>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fill, minmax(150px, 1fr))', gap: '8px', marginBottom: '20px' }}>
        {/* Funds that never have news of their own (cash and income funds) only add empty cards */}
        {groupMoods.filter((m) => m.articles > 0 || m.backgroundArticles > 0).map((m) => (
          <button
            key={m.symbol}
            type="button"
            onClick={() => setFilter('symbol', symbol === m.symbol ? '' : m.symbol)}
            style={{
              padding: '10px',
              textAlign: 'left',
              backgroundColor: 'white',
              border: `2px solid ${symbol === m.symbol ? COLORS.link : COLORS.border}`,
              borderRadius: '6px',
              cursor: 'pointer',
            }}
          >
            <div style={{ fontWeight: 'bold', marginBottom: '4px' }}>{m.symbol}</div>
            {m.articles > 0 ? (
              <SentimentChip label={m.label} score={m.score} confidence={m.confidence} compact />
            ) : (
              <span style={{ fontSize: '12px', color: COLORS.muted }}>No direct news</span>
            )}
            <div style={{ fontSize: '12px', color: COLORS.muted, marginTop: '4px' }}>
              {m.articles} {m.articles === 1 ? 'story' : 'stories'}{m.articles > 0 ? `, ${m.confidence}% sure` : ''}
            </div>
          </button>
        ))}
      </div>

      <div style={{ display: 'flex', flexWrap: 'wrap', gap: '10px', alignItems: 'center', marginBottom: '12px' }}>
        <select value={symbol} onChange={(e) => setFilter('symbol', e.target.value)} style={selectStyle} aria-label="Instrument">
          <option value="">All instruments</option>
          {groupMoods.map((m) => <option key={m.symbol} value={m.symbol}>{m.symbol}</option>)}
        </select>
        <select value={topic} onChange={(e) => setFilter('topic', e.target.value)} style={selectStyle} aria-label="Topic">
          <option value="">All topics</option>
          {topics.map((t) => <option key={t.key} value={t.key}>{t.label}</option>)}
        </select>
        <select value={mood} onChange={(e) => setFilter('mood', e.target.value)} style={selectStyle} aria-label="Mood">
          <option value="">Any mood</option>
          <option value="Positive">▲ Positive ({counts.Positive})</option>
          <option value="Negative">▼ Negative ({counts.Negative})</option>
          <option value="Neutral">● Neutral</option>
          {symbol && <option value="Background">○ Background</option>}
        </select>
        {backgroundCount > 0 && mood !== 'Background' && (
          <label style={{ fontSize: '13px', color: COLORS.muted, cursor: 'pointer' }}>
            <input type="checkbox" checked={showBackground} onChange={(e) => setShowBackground(e.target.checked)} />{' '}
            Include {backgroundCount} background {backgroundCount === 1 ? 'story' : 'stories'}<InfoTip term="background" />
          </label>
        )}
        {symbol && <Link to={`/instrument/${symbol}`} style={{ color: COLORS.link }}>Open {symbol} charts</Link>}
        <span style={{ color: COLORS.muted, fontSize: '13px' }}>{loading ? 'Loading...' : `${visible.length} stories in the last 30 days`}</span>
      </div>

      {error && <div style={{ color: COLORS.down, marginBottom: '12px' }}>{error}</div>}

      <div style={{ padding: '0 20px 8px', ...SURFACE }}>
        {!loading && visible.length === 0 && <p style={{ padding: '16px 0', color: COLORS.muted }}>No stories match these filters.</p>}
        {visible.slice(0, shown).map((a) => <ArticleCard key={a.id} article={a} symbol={symbol || undefined} />)}
        {visible.length > shown && (
          <button
            type="button"
            onClick={() => setShown(shown + PAGE_SIZE)}
            style={{ display: 'block', width: '100%', margin: '12px 0 8px', padding: '10px', border: `1px solid ${COLORS.border}`, borderRadius: '6px', backgroundColor: 'white', color: COLORS.link, cursor: 'pointer', fontSize: '14px' }}
          >
            Show more ({visible.length - shown} more)
          </button>
        )}
      </div>

      <p style={{ color: COLORS.muted, fontSize: '12px', marginTop: '12px' }}>
        Mood is read automatically from the wording of headlines and summaries. It can misjudge complicated or sarcastic headlines,
        and news around a price move suggests possible reasons but doesn't prove them.
      </p>
    </div>
  )
}
