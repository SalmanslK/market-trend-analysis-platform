import { Link } from 'react-router-dom'
import { glossary, GlossaryKey } from '../services/glossary'
import { COLORS, signalStyles, SURFACE } from '../services/format'
import { signalMeaning } from '../services/plainLanguage'
import { useFeatures } from '../services/features'
import { SignalAction } from '../services/types/marketData'

const sections: { title: string; terms: GlossaryKey[] }[] = [
  { title: 'Prices and performance', terms: ['price', 'change', 'range52w'] },
  { title: 'Risk', terms: ['volatility', 'drawdown'] },
  { title: 'Chart lines and indicators', terms: ['sma20', 'bollinger', 'trend', 'rsi', 'macd'] },
  { title: 'Trend and track record', terms: ['combinedSignal', 'signal', 'newsSignal', 'score', 'longTermTrend', 'backtest', 'goldenCross', 'monthlyTrend', 'annualReturn', 'tradingCost'] },
  { title: 'News', terms: ['newsMood', 'confidence', 'background', 'bigMoves'] },
]

const cardStyle = { padding: '20px', ...SURFACE, marginBottom: '12px', lineHeight: 1.55 }

const slug = (title: string) => title.toLowerCase().replace(/[^a-z0-9]+/g, '-')

/** The questions a first-time user asks, each answered in plain words. */
const QUICK_ANSWERS = (news: boolean): [string, string][] => [
  ['Where should I start?', 'On the Overview, read "Today at a glance": one sentence each for stocks, gold, the rupee and cash. Then open any instrument for its chart and what its signals mean.'],
  ['What do Buy, Hold and Sell mean?', 'Buy means prices have mostly been rising by the measures the app uses, Sell that they have mostly been falling, and Hold that the picture is mixed: stay as you are. They describe the past, not the future.'],
  ['Steady or daily signal: which should I use?', 'Steady (the default) averages the last 20 trading days, so it changes only a few times a year; it did better in testing. Daily reacts to every day and flips often, which costs money if you trade on it.'],
  ['Why do the signal and the long-term trend disagree?', 'They look at different periods. The signal reacts within weeks; the trend only turns when the 50-day average crosses the 200-day one, which takes months. After a sharp turn, they disagree for a while.'],
  ['What does "stronger than cash" mean?', 'Once a month the app checks whether an instrument earned more than the Meezan Cash Fund over the past 12 months. Staying invested only while it did was the one rule that passed a fair test written down in advance: on Meezan Islamic Fund since 2006 it earned about the same as holding, with the worst fall cut from about -60% to -25%. It switches about once a year. It was tested on stocks only.'],
  ['Should I just follow the signals?', 'In testing, simply holding usually earned more; the signals mainly helped avoid big falls. See Track Record for the numbers, and use the signals as a warning light rather than a reason to trade often.'],
  ['How do I follow my own investments?', 'Open My Holdings and add what you own. It shows the value, gain and payouts. Your list is saved only in this browser; nothing is sent anywhere.'],
  ...(news ? [['What does the news signal add?', 'It reads the tone of the last 7 days of headlines about an instrument. The combined signal says Buy or Sell only when news and the technical signal agree.'] as [string, string]] : []),
  ['Is this financial advice?', 'No. The app describes what prices have done. Think about your own goals, how long you invest for and how big a fall you could live with.'],
]

// Guide entries about news and sentiment, shown only when news is switched on
const NEWS_TERMS: GlossaryKey[] = ['combinedSignal', 'newsSignal', 'newsMood', 'confidence', 'background', 'bigMoves']

export default function Guide() {
  const { news } = useFeatures()
  const shown = sections
    .map((section) => ({ ...section, terms: section.terms.filter((t) => news || !NEWS_TERMS.includes(t)) }))
    .filter((section) => section.terms.length > 0)
  return (
    <div style={{ padding: '20px', maxWidth: '900px' }}>
      <h1>How to read this app</h1>
      <p style={{ color: COLORS.muted, margin: '6px 0 16px' }}>
        Start with the quick answers below. Every number and chart is explained further down, and you can tap any <strong>ⓘ</strong> in the app.
      </p>

      {/* The questions people actually ask, answered in a sentence or two, before the reference material */}
      <section aria-labelledby="quick-answers" style={{ ...cardStyle, borderTop: '4px solid #155e75' }}>
        <h2 id="quick-answers" style={{ fontSize: '1.2rem', marginBottom: '8px' }}>Quick answers</h2>
        {QUICK_ANSWERS(news).map(([question, answer]) => (
          <details key={question} style={{ borderTop: `1px solid ${COLORS.border}`, padding: '10px 0' }}>
            <summary style={{ cursor: 'pointer', fontWeight: 600, color: '#0e3b2e' }}>{question}</summary>
            <p style={{ marginTop: '6px', color: '#2f3d37' }}>{answer}</p>
          </details>
        ))}
      </section>

      <nav aria-label="On this page" style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', margin: '4px 0 16px', fontSize: '14px' }}>
        <span style={{ color: COLORS.muted }}>Jump to:</span>
        <a href="#how-to-use" style={{ color: COLORS.link }}>How to use it</a>
        <a href="#signals" style={{ color: COLORS.link }}>What the signals mean</a>
        {shown.map((section) => (
          <a key={section.title} href={`#${slug(section.title)}`} style={{ color: COLORS.link }}>{section.title}</a>
        ))}
        <a href="#important" style={{ color: COLORS.link }}>Important to know</a>
      </nav>

      <section style={cardStyle}>
        <h2 id="how-to-use" style={{ fontSize: '1.2rem', marginBottom: '8px' }}>A 2-minute way to use it</h2>
        <ol style={{ paddingLeft: '20px' }}>
          <li><Link to="/" style={{ color: COLORS.link }}>Market Overview</Link>: read "Today at a glance" for the big picture in one sentence per market, then pick what you'd like to do.</li>
          {news ? (<>
            <li>Open an instrument. Start with its plain summary, then the <strong>combined signal</strong>, which brings together the <strong>technical signal</strong> (what prices have been doing) and the <strong>news signal</strong> (what the headlines say).</li>
            <li>When they agree (e.g. technical and news both say Sell), the picture is clearer. When they disagree, be more careful.</li>
            <li>Check <strong>"Biggest moves and the news around them"</strong> to see what was happening when the price jumped or fell.</li>
          </>) : (<>
            <li>Open an instrument. Start with its plain summary and chart, then the <strong>technical signal</strong> (what prices have been doing) and the <strong>long-term trend</strong>.</li>
            <li>The technical signal and the trend can disagree after a turn: the signal reacts within weeks, the trend within months.</li>
          </>)}
          <li>Use <Link to="/comparison" style={{ color: COLORS.link }}>Compare</Link> to see which instrument did best over the same period, and <Link to="/portfolio" style={{ color: COLORS.link }}>Portfolio</Link> to test a mix.</li>
        </ol>
      </section>

      <section style={cardStyle}>
        <h2 id="signals" style={{ fontSize: '1.2rem', marginBottom: '8px' }}>What the signals mean</h2>
        {(Object.keys(signalMeaning) as SignalAction[]).map((action) => (
          <div key={action} style={{ display: 'flex', gap: '12px', alignItems: 'baseline', padding: '6px 0' }}>
            <span style={{
              minWidth: '90px', textAlign: 'center', padding: '2px 8px', borderRadius: '10px', fontWeight: 'bold', fontSize: '13px',
              backgroundColor: signalStyles[action].background, color: signalStyles[action].color,
            }}>
              {signalStyles[action].label}
            </span>
            <span><strong>{signalMeaning[action].headline}.</strong> {signalMeaning[action].detail}</span>
          </div>
        ))}
        <p style={{ marginTop: '8px' }}>
          The <Link to="/track-record" style={{ color: COLORS.link }}>Track Record</Link> page shows how acting on {news ? 'the technical, news and combined signals' : 'the technical signal and the trend rules'} would have done in the past.
          Each instrument's page also lists all the standard metrics, each with its own ⓘ explanation.
        </p>
      </section>

      {shown.map((section) => (
        <section key={section.title} style={cardStyle}>
          <h2 id={slug(section.title)} style={{ fontSize: '1.2rem', marginBottom: '8px' }}>{section.title}</h2>
          {section.terms.map((key) => (
            <div key={key} style={{ padding: '8px 0', borderTop: `1px solid ${COLORS.border}` }}>
              <strong>{glossary[key].title}</strong>
              <p>{glossary[key].what}</p>
              <p style={{ color: '#455a64' }}><em>How to read it:</em> {glossary[key].howToRead}</p>
            </div>
          ))}
        </section>
      ))}

      <section style={{ ...cardStyle, backgroundColor: '#fff8e1' }}>
        <h2 id="important" style={{ fontSize: '1.2rem', marginBottom: '8px' }}>Important to know</h2>
        <ul style={{ paddingLeft: '20px' }}>
          <li>Signals{news ? ', the trend and news mood' : ' and the trend'} describe what has happened; they cannot predict the future.</li>
          <li>Nothing here is financial advice. Think about your own goals, how long you plan to invest and how much loss you could accept.</li>
          <li>Meezan fund prices drop when the fund pays a dividend (payout). The app counts payouts as reinvested, so fund returns and charts don't show a payout as a loss; the latest price is the real price. Some of MIF's payouts before 2024 aren't published by the data source, so its long-term figures still understate it.</li>
          <li>KSE-100 and KMI-30 are price indices: they leave out the dividends paid by the companies in them, so holding them returned a bit more than shown.</li>
          <li>For USD/PKR, a rising number means the rupee is getting weaker.</li>
        </ul>
      </section>
    </div>
  )
}
