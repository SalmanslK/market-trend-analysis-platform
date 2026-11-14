import { useEffect, useState } from 'react'
import axios from 'axios'
import { briefApi, BriefStatus } from '../services/api/briefApi'
import { boldParts, BriefBlock, briefSections, parseBrief } from '../services/briefText'
import { ANSWER_SURFACE, COLORS, SURFACE } from '../services/format'

const when = (value: string) =>
  new Date(value).toLocaleString(undefined, { weekday: 'long', day: 'numeric', month: 'long', hour: 'numeric', minute: '2-digit' })

function Rich({ text }: { text: string }) {
  return <>{boldParts(text).map((part, i) => (part.bold ? <strong key={i}>{part.text}</strong> : <span key={i}>{part.text}</span>))}</>
}

function Blocks({ blocks }: { blocks: BriefBlock[] }) {
  return (
    <>
      {blocks.map((block, i) => {
        if (block.kind === 'list')
          return <ul key={i} style={{ paddingLeft: '22px', margin: '6px 0 10px' }}>{block.items.map((item, j) => <li key={j} style={{ marginBottom: '4px' }}><Rich text={item} /></li>)}</ul>
        if (block.kind === 'heading')
          return <h3 key={i} style={{ fontSize: '1rem', margin: '10px 0 4px' }}><Rich text={block.text} /></h3>
        return <p key={i} style={{ margin: '6px 0 10px' }}><Rich text={block.text} /></p>
      })}
    </>
  )
}

const sectionTitle = { fontSize: '1.15rem', marginTop: 0, marginBottom: '6px', color: COLORS.ink }

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
    <div style={{ padding: '20px' }}>
      <h1>Market brief</h1>
      <p className="no-print" style={{ color: COLORS.muted, marginBottom: '16px', lineHeight: 1.5, maxWidth: '860px' }}>
        Everything in one page, in plain words: what Pakistan's stocks, gold, the rupee and cash are doing, what is
        happening in the world economy, and what history says has tended to follow. Written by Claude from this
        app's figures and today's news, when you ask.
      </p>

      <section className="no-print" style={{ ...SURFACE, padding: '16px 18px', marginBottom: '20px', display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '12px' }}>
        <button
          type="button"
          onClick={write}
          disabled={writing || !status?.canWrite}
          style={{ padding: '10px 18px', fontSize: '16px', fontWeight: 700, borderRadius: '8px', border: 'none', cursor: writing ? 'wait' : 'pointer', color: 'white', backgroundColor: writing || !status?.canWrite ? '#7a8a83' : COLORS.ink }}
        >
          {writing ? 'Writing the brief…' : writtenToday ? 'Write a fresh brief' : "Write today's brief"}
        </button>
        {latest && !writing && (
          // The browser's print dialog offers "Save as PDF"; the print styles leave out everything but the brief
          <button
            type="button"
            title="Opens the print window: choose Save as PDF"
            onClick={() => {
              // The browser names the PDF after the page title
              const title = document.title
              document.title = `Market brief ${new Date(latest.createdAt).toLocaleDateString(undefined, { day: 'numeric', month: 'short', year: 'numeric' })}`
              window.print()
              document.title = title
            }}
            style={{ padding: '10px 18px', fontSize: '16px', fontWeight: 700, borderRadius: '8px', cursor: 'pointer', color: COLORS.ink, backgroundColor: 'white', border: `2px solid ${COLORS.ink}` }}
          >
            Export PDF
          </button>
        )}
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

      {latest && (() => {
        const [lead, ...rest] = briefSections(parseBrief(latest.text))
        return (
          // The first section across the top, the rest as cards filling the width; printed as one column
          <article aria-busy={writing} style={{ lineHeight: 1.6, fontSize: '16px' }}>
            <p style={{ color: COLORS.muted, fontSize: '14px', marginBottom: '8px' }}>Market brief, written {when(latest.createdAt)}</p>
            {lead && (
              <section className="print-plain" style={{ ...ANSWER_SURFACE, padding: '18px 22px', marginBottom: '14px' }}>
                {lead.title && <h2 style={sectionTitle}><Rich text={lead.title} /></h2>}
                <Blocks blocks={lead.blocks} />
              </section>
            )}
            <div className="brief-grid" style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(min(440px, 100%), 1fr))', gap: '14px', alignItems: 'start' }}>
              {rest.map((section) => (
                <section key={section.title} className="print-plain" style={{ ...SURFACE, padding: '16px 20px' }}>
                  <h2 style={sectionTitle}><Rich text={section.title} /></h2>
                  <Blocks blocks={section.blocks} />
                </section>
              ))}
            </div>

            <footer style={{ marginTop: '16px' }}>
              {latest.sources.length > 0 && (
                <section className="print-plain" style={{ ...SURFACE, padding: '14px 20px', marginBottom: '12px' }}>
                  <h2 style={{ fontSize: '1rem', marginTop: 0, marginBottom: '6px', color: COLORS.muted }}>Sources from the web</h2>
                  <ul style={{ paddingLeft: '22px', fontSize: '14px', columns: '2 320px', columnGap: '28px' }}>
                    {latest.sources.map((s) => (
                      <li key={s.url} style={{ marginBottom: '4px', overflowWrap: 'anywhere', breakInside: 'avoid' }}>
                        <a href={s.url} target="_blank" rel="noopener noreferrer" style={{ color: COLORS.link }}>{s.title}</a>
                      </li>
                    ))}
                  </ul>
                </section>
              )}
              <p style={{ fontSize: '14px', color: COLORS.muted }}>
                Written by AI from the app's figures and web news. It explains; it doesn't know the future, and it isn't
                financial advice. Figures come from the app, which you can check on each instrument's page.
              </p>
            </footer>
          </article>
        )
      })()}
    </div>
  )
}
