import { useEffect, useState } from 'react'
import axios from 'axios'
import { briefApi, BriefStatus } from '../services/api/briefApi'
import { boldParts, parseBrief } from '../services/briefText'
import { ANSWER_SURFACE, COLORS, SURFACE } from '../services/format'

const when = (value: string) =>
  new Date(value).toLocaleString(undefined, { weekday: 'long', day: 'numeric', month: 'long', hour: 'numeric', minute: '2-digit' })

function Rich({ text }: { text: string }) {
  return <>{boldParts(text).map((part, i) => (part.bold ? <strong key={i}>{part.text}</strong> : <span key={i}>{part.text}</span>))}</>
}

/**
 * The market brief: one page that explains what is happening and what history says may follow, written on request
 * by Claude from the app's own figures. The only AI in the app.
 */
export default function Brief() {
  const [status, setStatus] = useState<BriefStatus | null>(null)
  const [loadError, setLoadError] = useState(false)
  const [writing, setWriting] = useState(false)
  const [writeError, setWriteError] = useState<string | null>(null)

  useEffect(() => {
    briefApi.getLatest()
      .then(setStatus)
      .catch((err) => { console.error('Error loading the brief:', err); setLoadError(true) })
  }, [])

  const write = () => {
    setWriting(true)
    setWriteError(null)
    briefApi.write()
      .then((brief) => setStatus((s) => ({ canWrite: s?.canWrite ?? true, latest: brief })))
      .catch((err) => {
        const message = axios.isAxiosError(err) ? (err.response?.data as { message?: string } | undefined)?.message : undefined
        setWriteError(message ?? 'The brief could not be written. Try again in a few minutes.')
      })
      .finally(() => setWriting(false))
  }

  const latest = status?.latest
  const writtenToday = latest && new Date(latest.createdAt).toDateString() === new Date().toDateString()

  return (
    <div style={{ padding: '20px', maxWidth: '820px' }}>
      <h1>Market brief</h1>
      <p style={{ color: COLORS.muted, marginBottom: '16px', lineHeight: 1.5 }}>
        Everything in one page, in plain words: what Pakistan's stocks, gold, the rupee and cash are doing, what is
        happening in the world economy, and what history says has tended to follow. Written by Claude from this
        app's figures and today's news, when you ask.
      </p>

      <section style={{ ...SURFACE, padding: '16px 18px', marginBottom: '20px', display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '12px' }}>
        <button
          type="button"
          onClick={write}
          disabled={writing || !status?.canWrite}
          style={{ padding: '10px 18px', fontSize: '16px', fontWeight: 700, borderRadius: '8px', border: 'none', cursor: writing ? 'wait' : 'pointer', color: 'white', backgroundColor: writing || !status?.canWrite ? '#7a8a83' : COLORS.ink }}
        >
          {writing ? 'Writing the brief…' : writtenToday ? 'Write a fresh brief' : "Write today's brief"}
        </button>
        <span style={{ fontSize: '14px', color: COLORS.muted, flex: '1 1 260px' }}>
          {writing
            ? 'Reading the figures and searching the news. This takes two to five minutes; you can keep using the app.'
            : status && !status.canWrite
              ? 'Claude Code was not found on this computer. Install it and sign in with your Claude account to write briefs.'
              : 'Uses your Claude plan through Claude Code on this computer; there is no extra charge.'}
        </span>
      </section>

      {writeError && <div role="alert" style={{ ...SURFACE, padding: '12px 16px', marginBottom: '16px', color: COLORS.down }}>{writeError}</div>}
      {loadError && <div role="alert" style={{ color: COLORS.down, marginBottom: '16px' }}>Could not load the last brief. Is the app running?</div>}
      {status && !latest && !writing && <p style={{ color: COLORS.muted }}>No brief yet. Press the button above to write the first one.</p>}

      {latest && (
        <article style={{ ...ANSWER_SURFACE, padding: '20px 24px', lineHeight: 1.6, fontSize: '16px' }} aria-busy={writing}>
          <p style={{ color: COLORS.muted, fontSize: '14px', marginBottom: '8px' }}>Written {when(latest.createdAt)}</p>
          {parseBrief(latest.text).map((block, i) => {
            if (block.kind === 'heading')
              return <h2 key={i} style={{ fontSize: '1.15rem', marginTop: i === 0 ? 0 : '22px', marginBottom: '6px', color: COLORS.ink }}><Rich text={block.text} /></h2>
            if (block.kind === 'list')
              return <ul key={i} style={{ paddingLeft: '22px', margin: '6px 0 10px' }}>{block.items.map((item, j) => <li key={j} style={{ marginBottom: '4px' }}><Rich text={item} /></li>)}</ul>
            return <p key={i} style={{ margin: '6px 0 10px' }}><Rich text={block.text} /></p>
          })}

          {latest.sources.length > 0 && (
            <>
              <h2 style={{ fontSize: '1rem', marginTop: '22px', marginBottom: '6px', color: COLORS.muted }}>Sources from the web</h2>
              <ul style={{ paddingLeft: '22px', fontSize: '14px' }}>
                {latest.sources.map((s) => (
                  <li key={s.url} style={{ marginBottom: '4px', overflowWrap: 'anywhere' }}>
                    <a href={s.url} target="_blank" rel="noopener noreferrer" style={{ color: COLORS.link }}>{s.title}</a>
                  </li>
                ))}
              </ul>
            </>
          )}

          <p style={{ marginTop: '20px', paddingTop: '12px', borderTop: `1px solid ${COLORS.border}`, fontSize: '14px', color: COLORS.muted }}>
            Written by AI from the app's figures and web news. It explains; it doesn't know the future, and it isn't
            financial advice. Figures come from the app, which you can check on each instrument's page.
          </p>
        </article>
      )}
    </div>
  )
}
