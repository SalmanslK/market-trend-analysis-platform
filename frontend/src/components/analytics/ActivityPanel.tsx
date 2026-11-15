import { useEffect, useState } from 'react'
import { Bar, BarChart, Cell, ResponsiveContainer, Tooltip, XAxis, YAxis } from 'recharts'
import InfoTip from '../help/InfoTip'
import { analyticsApi } from '../../services/api/analyticsApi'
import { ActivitySeries, InstrumentAnalyticsDto } from '../../services/types/marketData'
import { bigNumber, fundSizeInWords, volumeInWords } from '../../services/plainLanguage'
import { COLORS, SURFACE } from '../../services/format'

const TONE_COLORS = {
  busy: { color: '#17663a', background: '#e8f5e9' },
  in: { color: '#17663a', background: '#e8f5e9' },
  quiet: { color: '#7a4f01', background: '#fbf1dc' },
  out: { color: '#a23a12', background: '#fdeee9' },
  usual: { color: '#2f3d37', background: '#e4e7eb' },
  flat: { color: '#2f3d37', background: '#e4e7eb' },
}

// Bars for days the price closed up, down, or flat; the same green and red as the rest of the app's up and down
const DAY_COLORS = { up: COLORS.up, down: COLORS.down, flat: '#9aa5ad' }
const dayColor = (direction?: number) => direction === 1 ? DAY_COLORS.up : direction === -1 ? DAY_COLORS.down : DAY_COLORS.flat
const DAY_WORDS: Record<string, string> = { '1': 'price closed up', '-1': 'price closed down', '0': 'price unchanged' }

const shortDate = (value: string) =>
  new Date(value).toLocaleDateString('en-US', { month: 'short', year: '2-digit', timeZone: 'UTC' })

/**
 * Trading volume for an index, or money in and out for a fund: one sentence in words and a small chart.
 * Shows nothing for instruments with neither (gold, the dollar).
 */
export default function ActivityPanel({ analytics }: { analytics: InstrumentAnalyticsDto }) {
  const [series, setSeries] = useState<ActivitySeries | null>(null)
  const symbol = analytics.symbol
  const words = analytics.volume ? volumeInWords(analytics.volume) : analytics.fundSize ? fundSizeInWords(analytics.fundSize) : null

  useEffect(() => {
    if (!words) return
    let cancelled = false
    setSeries(null)
    analyticsApi.getActivity(symbol)
      .then((data) => { if (!cancelled) setSeries(data) })
      .catch((err) => console.error('Error loading activity:', err))
    return () => { cancelled = true }
    // The words only exist when the analytics have activity, which changes with the symbol
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [symbol, !!words])

  if (!words) return null
  const isVolume = !!analytics.volume
  const tone = TONE_COLORS[words.tone]
  // Volume: the last six months (about 125 trading days), so each day's bar is wide enough to show its colour.
  // Fund sizes before about 2015 are tiny next to today's, so that chart shows the last ten years
  const points = (series?.points ?? []).slice(isVolume ? -125 : -120)

  return (
    <section aria-labelledby="activity-title" style={{ ...SURFACE, padding: '18px 20px', marginBottom: '12px' }}>
      <h2 id="activity-title" style={{ fontSize: '1.1rem', marginBottom: '8px' }}>
        {isVolume ? 'How busy trading is' : 'Money in and out'}
        <InfoTip term={isVolume ? 'volume' : 'fundFlows'} />
      </h2>
      <span style={{ display: 'inline-block', padding: '3px 12px', borderRadius: '999px', fontWeight: 700, fontSize: '16px', color: tone.color, backgroundColor: tone.background }}>
        {words.word}
      </span>
      <p style={{ fontSize: '14px', lineHeight: 1.5, marginTop: '8px', color: '#2f3d37' }}>
        {words.sentence} {isVolume
          ? 'Busy trading means many investors are taking part in the move; it doesn\'t say which way prices go next.'
          : 'This shows how keen investors are; it doesn\'t say which way the price goes next.'}
      </p>
      {points.length > 1 && (
        <div style={{ height: 160, marginTop: '10px' }} aria-label={isVolume ? 'Shares traded each day, last six months, green when the price closed up and red when down' : 'Fund size each month'}>
          <ResponsiveContainer width="100%" height="100%">
            <BarChart data={points} margin={{ top: 4, right: 8, bottom: 0, left: 0 }} barCategoryGap={0}>
              <XAxis dataKey="date" tickFormatter={shortDate} minTickGap={40} tick={{ fontSize: 12, fill: COLORS.muted }} />
              <YAxis tickFormatter={(v: number) => bigNumber(v).replace(' million', 'M').replace(' billion', 'B')} width={48} tick={{ fontSize: 12, fill: COLORS.muted }} />
              <Tooltip
                labelFormatter={(label: string) => new Date(label).toLocaleDateString('en-US', { day: isVolume ? 'numeric' : undefined, month: 'short', year: 'numeric', timeZone: 'UTC' })}
                formatter={(value: number, _name: string, item: { payload?: { direction?: number } }) => [
                  isVolume ? `${bigNumber(value)} shares, ${DAY_WORDS[String(item.payload?.direction ?? 0)]}` : `Rs ${bigNumber(value)}`,
                  isVolume ? 'Traded' : 'Fund size',
                ]}
              />
              <Bar dataKey="value" fill="#9cc6d3" isAnimationActive={false}>
                {isVolume && points.map((p) => <Cell key={p.date} fill={dayColor(p.direction)} />)}
              </Bar>
            </BarChart>
          </ResponsiveContainer>
        </div>
      )}
      {isVolume && points.length > 1 && (
        <p style={{ fontSize: '13px', color: COLORS.muted, marginTop: '6px', display: 'flex', flexWrap: 'wrap', gap: '4px 14px' }}>
          <span><span aria-hidden="true" style={{ display: 'inline-block', width: 10, height: 10, backgroundColor: DAY_COLORS.up, marginRight: 5 }} />Price closed up that day</span>
          <span><span aria-hidden="true" style={{ display: 'inline-block', width: 10, height: 10, backgroundColor: DAY_COLORS.down, marginRight: 5 }} />Price closed down</span>
          <span>Taller bar = more shares traded</span>
        </p>
      )}
    </section>
  )
}
