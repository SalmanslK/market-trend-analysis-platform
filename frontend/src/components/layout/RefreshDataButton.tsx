import { useState } from 'react'
import axios from 'axios'
import { dataApi, RefreshResult } from '../../services/api/dataApi'
import { useFeatures } from '../../services/features'
import { COLORS } from '../../services/format'

interface RefreshDataButtonProps {
  /** Called after new data arrived, so the page can reload what it shows */
  onRefreshed?: () => void
}

/** What a refresh found, in one sentence. */
function describeRefresh(r: RefreshResult, news: boolean): string {
  const updated = r.instruments.filter((i) => i.newPrices > 0)
  const failed = r.instruments.filter((i) => i.error)
  const payouts = r.instruments.reduce((sum, i) => sum + i.newPayouts, 0)
  const parts = [
    updated.length
      ? `New prices for ${updated.map((i) => i.symbol).join(', ')}`
      : 'No new prices yet: the sources haven\'t published anything newer',
    payouts ? `${payouts} new fund payout${payouts === 1 ? '' : 's'}` : '',
    !news ? '' : r.newsError ? 'news sources could not be read' : `${r.newStories} new news ${r.newStories === 1 ? 'story' : 'stories'}`,
  ].filter(Boolean)
  const problems = failed.length ? ` Could not update ${failed.map((i) => i.symbol).join(', ')}; the next check will try again.` : ''
  const record = r.signalRecordError ? " Today's signals could not be saved to the live record; the next check will try again." : ''
  return `${parts.join('; ')}.${problems}${record}`
}

/**
 * Checks every source for new prices, payouts and news now, instead of waiting for the Worker's next
 * scheduled check (prices every 6 hours, news every hour).
 */
export default function RefreshDataButton({ onRefreshed }: RefreshDataButtonProps) {
  const [running, setRunning] = useState(false)
  const { news } = useFeatures()
  const [message, setMessage] = useState<{ text: string; ok: boolean } | null>(null)

  const refresh = async () => {
    setRunning(true)
    setMessage(null)
    try {
      const result = await dataApi.refresh()
      setMessage({ text: describeRefresh(result, news), ok: !result.instruments.some((i) => i.error) })
      onRefreshed?.()
    } catch (err) {
      const busy = axios.isAxiosError(err) && err.response?.status === 409
      setMessage({
        text: busy
          ? 'A check is already running. Try again in a minute.'
          : 'Could not check for new data. Check that the API is running, then try again.',
        ok: false,
      })
    } finally {
      setRunning(false)
    }
  }

  return (
    <div style={{ display: 'flex', flexWrap: 'wrap', alignItems: 'center', gap: '8px 12px' }}>
      <button
        type="button"
        onClick={refresh}
        disabled={running}
        aria-busy={running}
        style={{
          padding: '6px 14px',
          border: `1px solid ${COLORS.link}`,
          borderRadius: '6px',
          backgroundColor: running ? '#e3f2fd' : 'white',
          color: COLORS.link,
          fontSize: '14px',
          fontWeight: 'bold',
          cursor: running ? 'wait' : 'pointer',
        }}
      >
        {running ? 'Checking sources...' : '↻ Check for new data now'}
      </button>
      <span role="status" style={{ fontSize: '13px', color: message && !message.ok ? COLORS.down : COLORS.neutral, flex: '1 1 260px' }}>
        {running ? `Reading every price${news ? ' and news' : ''} source; this can take up to a minute.` : message?.text}
      </span>
    </div>
  )
}
