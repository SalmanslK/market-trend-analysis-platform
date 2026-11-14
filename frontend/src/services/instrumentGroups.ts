import { InstrumentType } from './types/marketData'

/** The three groups the app is organised by, used by the overview tabs and the News page. */
export const INSTRUMENT_GROUPS = [
  { key: 'markets', title: 'Markets', note: 'Pakistan stock indices, gold and the dollar' },
  { key: 'funds', title: 'Funds', note: 'Meezan funds: shares and gold, then cash and income' },
  { key: 'shares', title: 'Shariah stocks', note: 'Ten companies with the best long-run records; see Rankings' },
] as const

export type InstrumentGroup = (typeof INSTRUMENT_GROUPS)[number]['key']

export function groupOfType(type: InstrumentType): InstrumentGroup {
  if (type === InstrumentType.Stock) return 'shares'
  if (type === InstrumentType.MutualFund) return 'funds'
  return 'markets'
}
