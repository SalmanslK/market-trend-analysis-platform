import { Link } from 'react-router-dom'
import { glossary, GlossaryKey } from '../services/glossary'
import { COLORS } from '../services/format'

const sections: { title: string; terms: GlossaryKey[] }[] = [
  { title: 'Prices and performance', terms: ['price', 'change', 'range52w'] },
  { title: 'Risk', terms: ['volatility', 'drawdown'] },
  { title: 'Chart lines and indicators', terms: ['sma20', 'bollinger', 'trend', 'rsi', 'macd'] },
  { title: 'Trend and track record', terms: ['longTermTrend', 'signal', 'backtest', 'goldenCross', 'monthlyTrend', 'annualReturn', 'tradingCost'] },
  { title: 'News', terms: ['newsMood', 'confidence', 'background', 'bigMoves'] },
]

const cardStyle = { padding: '20px', backgroundColor: 'white', borderRadius: '6px', marginBottom: '12px', lineHeight: 1.55 }

export default function Guide() {
  return (
    <div style={{ padding: '20px', maxWidth: '900px' }}>
      <h1>How to read this app</h1>
      <p style={{ color: COLORS.muted, margin: '6px 0 16px' }}>
        A plain-language guide to every number and chart. You can also hover or tap any <strong>ⓘ</strong> icon in the app.
      </p>

      <section style={cardStyle}>
        <h2 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>A 2-minute way to use it</h2>
        <ol style={{ paddingLeft: '20px' }}>
          <li><Link to="/" style={{ color: COLORS.link }}>Market Overview</Link>: read the "In plain words" box for the big picture, then look at which instruments are green or red.</li>
          <li>Open an instrument. Start with its plain summary, then the <strong>long-term trend</strong> (what prices have been doing) and the <strong>news mood</strong> (what the headlines say).</li>
          <li>When they agree (e.g. a downward trend and negative news), the picture is clearer. When they disagree, be more careful.</li>
          <li>Check <strong>"Biggest moves and the news around them"</strong> to see what was happening when the price jumped or fell.</li>
          <li>Use <Link to="/comparison" style={{ color: COLORS.link }}>Comparison</Link> to see which instrument did best over the same period.</li>
        </ol>
      </section>

      <section style={cardStyle}>
        <h2 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>Why there is no Buy or Sell label</h2>
        <p>
          The app used to show Buy or Sell. When tested on past prices, acting on it traded too often and did worse than simply holding,
          so it now shows the <strong>long-term trend</strong> instead: Up or Down. That tells you the direction so far, not what to do.
          The <Link to="/track-record" style={{ color: COLORS.link }}>Track Record</Link> page has the full results.
        </p>
      </section>

      {sections.map((section) => (
        <section key={section.title} style={cardStyle}>
          <h2 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>{section.title}</h2>
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
        <h2 style={{ fontSize: '1.2rem', marginBottom: '8px' }}>Important to know</h2>
        <ul style={{ paddingLeft: '20px' }}>
          <li>The trend and news mood describe what has happened; they cannot predict the future.</li>
          <li>Nothing here is financial advice. Think about your own goals, how long you plan to invest and how much loss you could accept.</li>
          <li>Meezan fund prices drop when the fund pays a dividend. The app doesn't adjust for this, so fund returns around payout dates look worse than they really were.</li>
          <li>For USD/PKR, a rising number means the rupee is getting weaker.</li>
        </ul>
      </section>
    </div>
  )
}
