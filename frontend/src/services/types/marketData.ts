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
  /** When the worker last checked the source successfully (a check can find nothing new) */
  lastUpdateDate?: string;
  /** The date of the newest stored price: what says whether the data is current */
  latestPriceDate?: string;
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
  /** Set when this is the steady signal shown in place of the daily one (see signalMode.ts) */
  steady?: boolean;
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
  /** "Cash fund" or "Income fund" when isCash */
  lowRiskKind?: string;
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
  /** The daily 5-vote score averaged over 20 trading days, -100 to +100; null with too little history */
  steadySignal?: ScaledSignal | null;
  /** The combined signal using the steady technical signal; null with steadySignal */
  steadyCombinedSignal?: ScaledSignal | null;
  /** Today's 5 votes, kept when the steady signal is shown in `signal` (see signalMode.ts) */
  dailySignal?: TechnicalSignal;
  metrics: Metric[];
  /** Fund cash payouts, newest first; prices and returns are adjusted for them */
  payouts: Payout[];
  payoutYield1Y?: number;
  /** The "stronger than cash" check; null for cash and income funds, currencies, dollar-priced gold and short histories */
  strongerThanCash?: CashTest | null;
}

/** Whether an instrument beat cash over the 12 months before this month's check (the first trading day) */
export interface CashTest {
  aheadOfCash: boolean;
  /** Percent, payouts included */
  return12M: number;
  /** The Meezan Cash Fund over the same 12 months, percent */
  cashReturn12M: number;
  checkedOn: string;
  /** Since which monthly check the answer has been the same; null if it hasn't changed in the stored history */
  since?: string | null;
}

/** The forward record: signals saved as they were shown, scored against what prices did next */
export interface SignalRecord {
  since?: string | null;
  readings: number;
  /** Readings old enough to have a full outcome */
  scored: number;
  horizonDays: number;
  groups: SignalRecordGroup[];
}

export interface SignalRecordGroup {
  /** Group names from SignalRecordSummary.cs */
  signal: 'Any day' | 'Technical signal (steady)' | 'Combined signal' | 'Stronger than cash';
  reading: string;
  count: number;
  /** Average move afterwards, percent */
  averageMove?: number | null;
  /** Share of readings followed by a rise, percent */
  shareUp?: number | null;
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

export type TradingRuleKey = 'signal' | 'steadySignal' | 'goldenCross' | 'monthlyTrend';

export interface RuleResult {
  key: TradingRuleKey;
  performance: Performance;
  verdict: BacktestVerdict;
  firstHalfAnnual: number;
  secondHalfAnnual: number;
}

export interface NewsRuleResult {
  key: 'technical' | 'news' | 'combined' | 'steadyTechnical' | 'steadyCombined';
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
  equityCurve: { date: string; holding: number; technical: number; news: number; combined: number; steadyTechnical: number; steadyCombined: number }[];
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
  /** The same outcomes and spreads for the steady technical signal */
  steadyOutcomes: SignalOutcome[];
  steadyBuyMinusSell30D?: number;
  steadyBuyMinusSell30DFirstHalf?: number;
  steadyBuyMinusSell30DSecondHalf?: number;
  rules: RuleResult[];
  newsTest: NewsBacktest;
  equityCurve: { date: string; buyAndHold: number; strategy: number; goldenCross: number; monthlyTrend: number; steadySignal: number }[];
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
  instruments: { symbol: string; name: string; firstDate: string; isCash: boolean; lowRiskKind?: string }[];
}

/** A signal or trend change from one day's close to the next */
export interface SignalAlert {
  date: string;
  symbol: string;
  name: string;
  signal: 'Combined' | 'Technical' | 'News' | 'Trend';
  /** A Buy/Sell action, or Up/Down for the trend */
  from: string;
  to: string;
  /** The combined signal turned to Buy or Sell, or the trend changed */
  important: boolean;
}
