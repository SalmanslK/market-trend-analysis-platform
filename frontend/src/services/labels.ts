import { Interval, Timeframe, InstrumentType } from './types/marketData'

export const intervalLabels: Record<Interval, string> = {
  [Interval.Daily]: 'Daily',
  [Interval.Weekly]: 'Weekly',
  [Interval.Monthly]: 'Monthly',
  [Interval.Quarterly]: 'Quarterly',
  [Interval.Yearly]: 'Yearly',
}

export const timeframeLabels: Record<Timeframe, string> = {
  [Timeframe.Day]: 'Day',
  [Timeframe.Week]: 'Week',
  [Timeframe.Month]: 'Month',
  [Timeframe.Quarter]: 'Quarter',
  [Timeframe.HalfYear]: 'Half-Year',
  [Timeframe.Year]: 'Year',
  [Timeframe.Ytd]: 'YTD',
  [Timeframe.All]: 'All',
}

export const instrumentTypeLabels: Record<InstrumentType, string> = {
  [InstrumentType.StockIndex]: 'Stock Index',
  [InstrumentType.MutualFund]: 'Mutual Fund',
  [InstrumentType.Forex]: 'Forex',
  [InstrumentType.Commodity]: 'Commodity',
}
