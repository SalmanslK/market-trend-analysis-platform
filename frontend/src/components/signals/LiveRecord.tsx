import { useEffect, useState } from 'react'
import { signalRecordApi } from '../../services/api/signalRecordApi'
import { SignalRecord } from '../../services/types/marketData'
import { changeColor, COLORS, formatDate, formatPercent, SURFACE } from '../../services/format'
import { useFeatures } from '../../services/features'
import InfoTip from '../help/InfoTip'

const cellStyle = { padding: '8px 10px', borderBottom: `1px solid ${COLORS.border}`, textAlign: 'right' as const }
const sectionStyle = { ...SURFACE, padding: '20px', marginTop: '12px' }

/**
 * The forward record: the signals saved each day as they were shown, and what prices did over the following weeks.
 * Unlike the backtests on this page, it can't be fitted to the past, because each reading is saved before its
 * outcome exists.
 */
export default function LiveRecord() {
  const [record, setRecord] = useState<SignalRecord | null>(null)
  const [error, setError] = useState(false)
  // The combined signal includes news, so its rows are hidden when news is switched off
  const { news } = useFeatures()

  useEffect(() => {
    let cancelled = false
    signalRecordApi.get()
      .then((data) => { if (!cancelled) setRecord(data) })
      .catch((err) => {
        console.error('Error loading the live record:', err)
        if (!cancelled) setError(true)
      })
    return () => { cancelled = true }
  }, [])

  const heading = (
    <h2 id="live-record" style={{ fontSize: '1.2rem', marginBottom: '6px' }}>Live record: the signals since we started saving them<InfoTip term="liveRecord" /></h2>
  )
  if (error) {
    return (
      <section aria-labelledby="live-record" style={sectionStyle}>
        {heading}
        <p role="alert" style={{ color: COLORS.down }}>The live record couldn't be loaded. Reload the page; if it keeps failing, the API log says why.</p>
      </section>
    )
  }
  if (!record) return null

  const groups = record.groups.filter((g) => g.count > 0 && (news || g.signal !== 'Combined signal'))
  return (
    <section aria-labelledby="live-record" style={sectionStyle}>
      {heading}
      <p style={{ fontSize: '14px', color: '#334e68', lineHeight: 1.5 }}>
        Every day the app saves each instrument's signals as shown, before anyone knows what happens next. Each reading is then scored by
        how prices moved over the {record.horizonDays} trading days after the next day's close. This is the honest test: unlike the replays
        above, it can't be fitted to the past.
      </p>
      {record.readings === 0 ? (
        <p style={{ marginTop: '10px', color: COLORS.muted }}>Nothing saved yet. The first readings are saved at the next price update.</p>
      ) : record.scored === 0 ? (
        <p style={{ marginTop: '10px', color: COLORS.muted }}>
          Saving since {formatDate(record.since!)}: {record.readings} readings so far. The first results appear once {record.horizonDays} trading
          days have passed after a reading (about a month).
        </p>
      ) : (<>
        <p style={{ marginTop: '10px', fontSize: '14px', color: COLORS.muted }}>
          Since {formatDate(record.since!)}: {record.readings} readings saved, {record.scored} with a result. Instruments that move together
          (the stock indices and stock funds) count separately, so the real evidence is thinner than the counts suggest; give it a year.
        </p>
        <div style={{ overflowX: 'auto', marginTop: '8px' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '14px' }}>
            <thead>
              <tr>
                <th style={{ ...cellStyle, textAlign: 'left' }}>When the app said</th>
                <th style={cellStyle}>Readings</th>
                <th style={cellStyle}>Average move over the next {record.horizonDays} days</th>
                <th style={cellStyle}>Followed by a rise</th>
              </tr>
            </thead>
            <tbody>
              {groups.map((g) => (
                <tr key={`${g.signal}-${g.reading}`}>
                  <td style={{ ...cellStyle, textAlign: 'left' }}>{g.signal === 'Any day' ? 'Any day (for comparison)' : `${g.signal}: ${g.reading}`}</td>
                  <td style={cellStyle}>{g.count}</td>
                  <td style={{ ...cellStyle, color: changeColor(g.averageMove) }}>{formatPercent(g.averageMove)}</td>
                  <td style={cellStyle}>{g.shareUp == null ? '-' : `${g.shareUp.toFixed(0)}%`}</td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      </>)}
    </section>
  )
}
