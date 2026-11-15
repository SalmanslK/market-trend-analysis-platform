import { SignalAction } from './types/marketData'

/**
 * The app's colours. Text colours meet the WCAG AA contrast ratio (4.5:1) on white, the page grey (#f5f5f5) and the
 * light tints used for boxes, so muted text and links stay readable.
 */
export const COLORS = {
  up: '#17663a',
  down: '#b3261e',
  neutral: '#5b6560',
  muted: '#55605a',
  border: '#d8ddd8',
  // Deep teal-blue: clearly a link, and never confused with the green that means "up"
  link: '#155e75',
  ink: '#0e3b2e',
  text: '#2f3d37',
  tint: '#eef2ee',
}

/** The look of every card: white paper on the page, with a hairline rule rather than a shadow */
export const SURFACE = {
  backgroundColor: 'white',
  borderRadius: '10px',
  border: `1px solid ${COLORS.border}`,
}

/** The page's main answer: the same paper, marked by an ink bar so it reads first */
export const ANSWER_SURFACE = {
  ...SURFACE,
  borderLeft: `5px solid ${COLORS.ink}`,
}

/** 166409.25 -> "166,409.25" */
export function formatNumber(value: number | undefined | null, decimals = 2): string {
  if (value == null || Number.isNaN(value)) return '-'
  return value.toLocaleString(undefined, { minimumFractionDigits: decimals, maximumFractionDigits: decimals })
}

/** 2.345 -> "+2.35%" */
export function formatPercent(value: number | undefined | null, decimals = 2): string {
  if (value == null || Number.isNaN(value)) return '-'
  // Tiny changes that round to zero shouldn't show a "+"
  const rounded = Number(value.toFixed(decimals))
  return `${rounded > 0 ? '+' : ''}${(rounded === 0 ? 0 : value).toFixed(decimals)}%`
}

export function changeColor(value: number | undefined | null): string {
  // Anything that displays as 0.00% is shown as unchanged
  if (value == null || Math.abs(value) < 0.005) return COLORS.neutral
  return value > 0 ? COLORS.up : COLORS.down
}

export function formatDate(value: string): string {
  return new Date(value).toLocaleDateString(undefined, { year: 'numeric', month: 'short', day: 'numeric' })
}

/** Whole days from a date to now. */
export function daysSince(value: string): number {
  return Math.floor((Date.now() - new Date(value).getTime()) / 86_400_000)
}

// A weekend plus a holiday can leave prices 4 days old; more than that means the updates have stopped
export const STALE_AFTER_DAYS = 4

export const signalStyles: Record<SignalAction, { label: string; background: string; color: string }> = {
  StrongBuy: { label: 'Strong Buy', background: '#1b5e20', color: 'white' },
  Buy: { label: 'Buy', background: '#c8e6c9', color: '#1b5e20' },
  Hold: { label: 'Hold', background: '#eeeeee', color: '#424242' },
  Sell: { label: 'Sell', background: '#ffcdd2', color: '#b71c1c' },
  StrongSell: { label: 'Strong Sell', background: '#b71c1c', color: 'white' },
}

export const TREND_DISCLAIMER =
  'Signals, trends and indicators describe past prices. They are not predictions or financial advice, and do not account for fundamentals. Fund prices include payouts; index prices leave out dividends.'
