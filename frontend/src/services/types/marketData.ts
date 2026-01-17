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

export interface PriceDataDto {
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