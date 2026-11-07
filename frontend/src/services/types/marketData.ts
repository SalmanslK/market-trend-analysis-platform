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

export interface LongTermTrend {
  direction: 'Up' | 'Down' | 'Unknown';
  /** The day the trend last changed; absent if it hasn't changed in the stored history */
  since?: string;
  /** The price has already turned against the trend; the trend changes only when the averages cross */
  phase?: 'Recovering' | 'Weakening';
}

export interface Metric {
  group: string;
  name: string;
  value: number;
  unit: 'price' | 'percent' | 'number';
  /** Positive, Negative or Neutral; empty when the metric has no good or bad direction */
  reading: string;
  note: string;
  description: string;
}

export interface ScaledSignal {
  action: SignalAction;
  /** -100 to +100 */
  score: number;
  articles: number;
  newsConfidence: number;
  /** Other instruments with exactly the same news reading (they are linked to the same stories) */
  sharedWith: string[];
}

export interface InstrumentAnalyticsDto {
  symbol: string;
  name: string;
  type: InstrumentType;
  /** A cash (money market) fund: Buy/Sell signals don't apply */
  isCash: boolean;
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
  longTermTrend: LongTermTrend;
  signal: TechnicalSignal;
  newsSignal: ScaledSignal;
  combinedSignal: ScaledSignal;
  metrics: Metric[];
  /** Fund cash payouts, newest first; prices and returns are adjusted for them */
  payouts: Payout[];
  payoutYield1Y?: number;
}

export interface Payout {
  date: string;
  amountPerUnit: number;
}

export type SentimentLabel = 'Positive' | 'Neutral' | 'Negative' | 'Background';

export interface NewsTopic {
  key: string;
  label: string;
  whyItMatters: string;
  symbols: string[];
}

export interface ArticleSentiment {
  score: number;
  confidence: number;
  label: SentimentLabel;
  positiveWords: string[];
  negativeWords: string[];
}

export interface InstrumentChange {
  symbol: string;
  tradingDate?: string;
  change?: number;
  /** Directly about this instrument; otherwise background context with no score */
  isDirect: boolean;
  sentimentScore: number;
  sentimentLabel: SentimentLabel;
}

export interface NewsArticle {
  id: number;
  source: string;
  title: string;
  summary: string;
  url: string;
  publishedAt: string;
  topics: NewsTopic[];
  sentiment: ArticleSentiment;
  instruments: InstrumentChange[];
}

export interface NewsSentimentSummary {
  symbol: string;
  days: number;
  articles: number;
  backgroundArticles: number;
  positive: number;
  neutral: number;
  negative: number;
  score: number;
  confidence: number;
  label: SentimentLabel;
}

export interface PriceMove {
  date: string;
  price: number;
  change: number;
  timesTypical?: number;
  newsFrom: string;
  newsTo: string;
  articles: NewsArticle[];
}

export interface InstrumentMoves {
  symbol: string;
  typicalDailyMove?: number;
  newsAvailableSince?: string;
  moves: PriceMove[];
}

export type BacktestVerdict = 'Helped' | 'LowerRisk' | 'DidNotHelp';

export interface Performance {
  totalReturn: number;
  annualReturn: number;
  maxDrawdown: number;
  trades: number;
  timeInvested: number;
}

export interface HorizonOutcome {
  horizonDays: number;
  samples: number;
  averageChange: number;
  medianChange: number;
  percentPositive: number;
}

export interface SignalOutcome {
  action: SignalAction | 'All';
  days: number;
  periods: number;
  horizons: HorizonOutcome[];
}

export type TradingRuleKey = 'signal' | 'goldenCross' | 'monthlyTrend';

export interface RuleResult {
  key: TradingRuleKey;
  performance: Performance;
  verdict: BacktestVerdict;
  firstHalfAnnual: number;
  secondHalfAnnual: number;
}

export interface NewsRuleResult {
  key: 'technical' | 'news' | 'combined';
  performance: Performance;
  verdict: BacktestVerdict;
  buyMinusSell30D?: number;
  buyDays: number;
  sellDays: number;
}

export interface NewsBacktest {
  hasEnoughNews: boolean;
  startDate?: string;
  endDate?: string;
  years: number;
  tradingDays: number;
  articles: number;
  coveragePercent: number;
  holding: Performance;
  rules: NewsRuleResult[];
  equityCurve: { date: string; holding: number; technical: number; news: number; combined: number }[];
}

export interface BacktestResult {
  symbol: string;
  name: string;
  type: InstrumentType;
  hasEnoughData: boolean;
  startDate: string;
  endDate: string;
  years: number;
  tradingDays: number;
  costPercent: number;
  cashInterestPercent: number;
  verdict: BacktestVerdict;
  buyAndHold: Performance;
  strategy: Performance;
  strategyWithInterest: Performance;
  outcomes: SignalOutcome[];
  buyMinusSell30D?: number;
  buyMinusSell30DFirstHalf?: number;
  buyMinusSell30DSecondHalf?: number;
  holdingFirstHalfAnnual: number;
  holdingSecondHalfAnnual: number;
  rules: RuleResult[];
  newsTest: NewsBacktest;
  equityCurve: { date: string; buyAndHold: number; strategy: number; goldenCross: number; monthlyTrend: number }[];
}

export type RebalanceSchedule = 'Never' | 'Yearly' | 'HalfYearly' | 'Quarterly'

/** A mix to test: weights in percent per symbol (the rest is cash) */
export interface PortfolioMix {
  name: string;
  weights: Record<string, number>;
  rebalance: RebalanceSchedule;
  trendFilter: boolean;
}

export interface PortfolioResult extends PortfolioMix {
  costPercent: number;
  startDate: string;
  endDate: string;
  years: number;
  totalReturn: number;
  annualReturn: number;
  maxDrawdown: number;
  volatility: number;
  /** Return above cash per unit of volatility */
  sharpe?: number;
  yearlyReturns: { year: number; return: number }[];
  trades: number;
  equityCurve: { date: string; value: number }[];
}

export interface PortfolioComparison {
  startDate: string;
  endDate: string;
  costPercent: number;
  cashAnnualReturn?: number;
  results: PortfolioResult[];
  instruments: { symbol: string; name: string; firstDate: string; isCash: boolean }[];
}
