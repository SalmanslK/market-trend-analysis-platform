import { InstrumentAnalyticsDto } from '../../services/types/marketData'
import { COLORS, formatDate, TREND_DISCLAIMER, signalStyles } from '../../services/format'
import { describeTrend, directionNote, signalMeaning } from '../../services/plainLanguage'
import InfoTip from '../help/InfoTip'
import TrendBadge from '../trend/TrendBadge'
import ScaledSignalBadge from './ScaledSignalBadge'
import SignalBadge from './SignalBadge'
import SignalTrackRecord from './SignalTrackRecord'

interface SignalPanelProps {
  analytics: InstrumentAnalyticsDto
}

const impactStyle = (impact: number) =>
  impact > 0
    ? { icon: '▲', color: COLORS.up, label: 'Bullish' }
    : impact < 0
      ? { icon: '▼', color: COLORS.down, label: 'Bearish' }
      : { icon: '●', color: COLORS.neutral, label: 'Neutral' }

export default function SignalPanel({ analytics }: SignalPanelProps) {
  const { signal, longTermTrend } = analytics
  const note = directionNote(analytics.symbol)

  return (
    <section style={{ padding: '20px', backgroundColor: 'white', borderRadius: '6px' }}>
      <h2 style={{ fontSize: '1.2rem', marginBottom: '10px' }}>Buy/Sell signals</h2>
      <div style={{ display: 'grid', gridTemplateColumns: 'repeat(auto-fit, minmax(150px, 1fr))', gap: '8px', marginBottom: '14px' }}>
        <div style={{ padding: '10px 12px', border: `2px solid ${COLORS.border}`, borderRadius: '6px' }}>
          <div style={{ fontSize: '12px', color: COLORS.muted, marginBottom: '6px' }}>Combined<InfoTip term="combinedSignal" /></div>
          <ScaledSignalBadge signal={analytics.combinedSignal} large />
        </div>
        <div style={{ padding: '10px 12px', border: `1px solid ${COLORS.border}`, borderRadius: '6px' }}>
          <div style={{ fontSize: '12px', color: COLORS.muted, marginBottom: '6px' }}>Technical{signal.steady ? ' (steady)' : ' (daily)'}<InfoTip term="signal" /></div>
          <SignalBadge signal={signal} large />
        </div>
        <div style={{ padding: '10px 12px', border: `1px solid ${COLORS.border}`, borderRadius: '6px' }}>
          <div style={{ fontSize: '12px', color: COLORS.muted, marginBottom: '6px' }}>News, last 7 days<InfoTip term="newsSignal" /></div>
          <ScaledSignalBadge signal={analytics.newsSignal} large />
          <div style={{ fontSize: '12px', color: COLORS.muted, marginTop: '6px' }}>
            {analytics.newsSignal.articles} {analytics.newsSignal.articles === 1 ? 'story' : 'stories'}, {analytics.newsSignal.newsConfidence}% sure
          </div>
          {analytics.newsSignal.sharedWith.length > 0 && (
            <div style={{ fontSize: '12px', color: COLORS.muted, marginTop: '4px' }}>
              Same news as {analytics.newsSignal.sharedWith.join(', ')}: not specific to {analytics.symbol}
            </div>
          )}
        </div>
      </div>

      <h3 style={{ fontSize: '1rem', marginBottom: '6px' }}>Why the technical signal says this</h3>
      <p style={{ marginBottom: '6px', lineHeight: 1.45 }}>
        <strong>{signalMeaning[signal.action].headline}.</strong> {signalMeaning[signal.action].detail}
      </p>
      <p style={{ color: COLORS.muted, fontSize: '13px', marginBottom: '12px' }}>
        {signal.steady && analytics.dailySignal ? (
          <>
            This is the steady signal: the daily vote count averaged over the last 20 trading days, {signal.score > 0 ? '+' : ''}{signal.score} on a scale of -100 to +100.<InfoTip term="steadySignal" />{' '}
            Today the indicators below give {analytics.dailySignal.score > 0 ? '+' : ''}{analytics.dailySignal.score} of {analytics.dailySignal.maxScore} votes,
            which on its own would say {signalStyles[analytics.dailySignal.action].label}. Based on daily prices up to {formatDate(analytics.lastDate)}.
          </>
        ) : (
          <>
            Each indicator below votes ▲ up, ▼ down or ● no view, giving {signal.score > 0 ? '+' : ''}{signal.score} of {signal.maxScore} votes.<InfoTip term="score" /> Based on daily prices up to {formatDate(analytics.lastDate)}.
          </>
        )}
      </p>

      <ul style={{ listStyle: 'none' }}>
        {signal.reasons.map((reason) => {
          const style = impactStyle(reason.impact)
          return (
            <li
              key={reason.indicator}
              style={{ display: 'flex', gap: '10px', padding: '8px 0', borderTop: `1px solid ${COLORS.border}` }}
            >
              <span style={{ color: style.color, width: '16px' }} aria-label={style.label}>{style.icon}</span>
              <span style={{ fontWeight: 'bold', minWidth: '150px' }}>{reason.indicator}</span>
              <span>{reason.detail}</span>
            </li>
          )
        })}
      </ul>

      <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '12px', marginTop: '16px' }}>
        <h3 style={{ fontSize: '1rem' }}>Long-term trend<InfoTip term="longTermTrend" /></h3>
        <TrendBadge trend={longTermTrend} />
      </div>
      <p style={{ lineHeight: 1.45, marginTop: '6px', fontSize: '14px' }}>{describeTrend(analytics)}</p>
      {note && <p style={{ lineHeight: 1.45, marginTop: '6px', fontSize: '13px', color: '#546e7a' }}>{note}</p>}

      <SignalTrackRecord symbol={analytics.symbol} />

      <p style={{ color: COLORS.muted, fontSize: '12px', marginTop: '12px' }}>{TREND_DISCLAIMER}</p>
    </section>
  )
}
