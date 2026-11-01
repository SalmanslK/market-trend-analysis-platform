export enum Interval {
  Daily = 0,
  Weekly = 1,
  Monthly = 2,
  Quarterly = 3,
  Yearly = 4,
}

export enum Timeframe {
  Day = 0,
  Week = 1,
  Month = 2,
  Quarter = 3,
  HalfYear = 4,
  Year = 5,
  Ytd = 6,
  All = 7,
}

export enum InstrumentType {
  StockIndex = 0,
  MutualFund = 1,
  Forex = 2,
  Commodity = 3,
}

export interface PriceDataDto extends PriceIndicatorFields {
  dateTime: string;
  price: number;
  sma20?: number;
  upperBand?: number;
  lowerBand?: number;
  ma4W?: number;
  ma6M?: number;
  ma12M?: number;
}

export interface InstrumentDto {
  id: number;
  symbol: string;
  name: string;
  type: InstrumentType;
  isActive: boolean;
  lastUpdateDate?: string;
}

export interface SummaryDto {
  instrument: string;
  date: string;
  price: number;
  sma20?: number;
  upperBand?: number;
  lowerBand?: number;
}
export interface PriceIndicatorFields {
  rsi14?: number;
  macd?: number;
  macdSignal?: number;
  macdHistogram?: number;
}

export type SignalAction = 'StrongBuy' | 'Buy' | 'Hold' | 'Sell' | 'StrongSell';

export interface SignalReason {
  indicator: string;
  /** +1 bullish, -1 bearish, 0 neutral */
  impact: number;
  detail: string;
}

export interface TechnicalSignal {
  action: SignalAction;
  score: number;
  maxScore: number;
  reasons: SignalReason[];
}

export interface InstrumentAnalyticsDto {
  symbol: string;
  name: string;
  type: InstrumentType;
  lastDate: string;
  lastPrice: number;
  dataPoints: number;
  change1D?: number;
  change1W?: number;
  change1M?: number;
  change3M?: number;
  changeYtd?: number;
  change1Y?: number;
  high52W?: number;
  low52W?: number;
  rangePosition52W?: number;
  volatility1Y?: number;
  maxDrawdown1Y?: number;
  rsi14?: number;
  macd?: number;
  macdSignal?: number;
  macdHistogram?: number;
  sma50?: number;
  sma200?: number;
  percentB?: number;
  signal: TechnicalSignal;
}
