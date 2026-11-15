# Project tracker

What has been built, what is left, and what was decided against. Updated 15 November 2026.
`SETUP_CHECKLIST.md` has the detail for each feature and every research result; this file is the overview.

## Status at a glance

| | |
|---|---|
| Code | Everything merged into `main` (PRs #1 to #9) |
| App | Runs locally: `scripts/start-app.ps1`, website at http://localhost:3000 |
| Morning brief | Cloud routine, weekdays 8:30 am Pakistan time, to the phone (Claude app) |
| Tests | Backend 221, website 35, all passing |

## Left to do

| Item | Who | When |
|---|---|---|
| Check the morning brief arrives by itself (first scheduled run) | User | Mon 16 Nov 2026, about 8:30 am |
| If the Claude-app skill says the price site is blocked: allow `beta-restapi.sarmaaya.pk` and `news.google.com` for code execution (claude.ai, Settings, Capabilities) | User | Only if needed |
| First Live record results appear on Track Record (needs 20 trading days of saved signals) | Automatic | About early December 2026 |
| Judge the Live record properly; rerun the signal research on prices after November 2026 (the first truly unseen test) | Developer | About November 2027 |
| Optional: full with/without comparison of the brief skill (test cases are in the skill's `evals/`; costs about four briefs of plan usage) | Developer | Any time |
| Optional: delete the merged branches `feature/guided-ui` and `feature/volumes` on GitHub | User's call | Any time |

## Done

### Foundations (PRs #1 to #7, before November 2026)
- Prices for the KSE-100, KMI-30, Meezan funds (MIF, KMIF, MBF, MGF and the cash and income funds), gold (Pakistan and
  international) and USD/PKR; charts, indicators, rule-based technical signal and long-term trend.
- Plain-language summaries, a Guide, news collection with a rule-based mood, combined (technical and news) signal.
- Backtests (Track Record), portfolio mixes, alerts for changes, fund payouts reinvested in all returns.
- Start, stop and daily backup scripts; CI for backend and website.

### Research and honesty (PR #8, November 2026)
- Every test written down and committed before running, checked once on held-back years (`scripts/research/`).
- Results: technical signals, news mood, the State Bank rate and volatility targeting did **not** beat holding;
  **stronger than cash** (stay in stocks only while their last 12 months beat cash) **passed both periods**.
- Stronger-than-cash check shown in the app; a forward **Live record** saves each day's signals to score them later.
- Guided design (answer first), design pass, code and PR reviews with all findings fixed, `CLAUDE.md`.

### Volumes, history and the brief (PR #9, November 2026)
| Area | What | Commit |
|---|---|---|
| Volumes | Daily KSE-100 and KMI-30 trading volume (from 2013) and monthly fund sizes, kept current | 46d7d25 |
| History | Older daily prices added where the source has them (KSE-100 from 2002, KMI-30 2009, MBF 2005...) | c207477 |
| Research | Volume and fund-flow tests, pre-registered: no volume rule beat holding | b562b06, a0413c9 |
| Pages | "How busy trading is" and "Money in and out" panels | cc12b90 |
| Brief | Market brief page, written on request by Claude Code on the user's plan (no API key); history-based outlook | ad97688 |
| Brief | Export PDF; cards across the page width, aligned | c6f5325, 778806b, b111e72 |
| Stocks | Shariah stock rankings (pre-registered, two labelled corrections to the split rule); best industry; final 10 | 9f812cf, b2bf389 |
| Stocks | Ten Shariah stocks followed like the funds (GHNI, SYS, SAZEW, SRVI, PIOC, JVDC, MARI, FATIMA, MEBL, DCR); split and bonus adjustment; Rankings page | 59a69c4 |
| Stocks | KMI-30 badges (live membership); every KMI-30 company ranked on the Rankings page | d887fb2, 96604da |
| Overview | Three tabs: Markets (Pakistan, Global), Funds, Shariah stocks | d887fb2 |
| Speed | Answers kept until new data arrives and refreshed in the background: overview 13 s to 0.2 s, Track Record 19 s, Portfolio 5 s | d887fb2, 11a343f |
| News | News page by group with combined moods (Pakistan, the dollar, global, funds, each industry) | be01441 |
| News | Company news read from the company's point of view (a discovery is the company's news, not gold's) | be01441 |
| News | The same event carried by many outlets counts once | c9dba9a |
| News | Company data pages ("SYS Technical Analysis ...") left out; the 32 already stored deleted | d887fb2 |
| Review | Every page checked: Guide covers every term, Compare grouped and phone-safe, Settings explains the phone brief | 811422d |
| Phone | Cloud brief: facts gathered without the PC's database, same writing rules (a test keeps them equal) | 839277f |
| Phone | Skill `pakistan-market-brief` (Claude Code here; uploaded to the user's Claude account); retries on source hiccups | 14aa700, 46bdf4d |

### Outside the repository
- **Morning brief routine** "Morning market brief (Pakistan)": https://claude.ai/code/routines/trig_01LvU3hwaC9wUAK8veBv5iWq
  - Weekdays 8:30 am Pakistan time (after the Pakistan and US closes, an hour before the PSX opens).
  - Saves the brief as a Claude document ("Market brief, &lt;date&gt;") and sends a phone notification with its link.
  - Its cloud environment ("Default") allows `beta-restapi.sarmaaya.pk` and `news.google.com`.
  - First full test: "Market brief, 13 November 2026", https://claude.ai/artifact/QuoiboSgQGpZ6gvenXGd7j
- **Skill** uploaded to the user's Claude account (claude.ai, Customize, Skills).

## Decided not to do
| Idea | Why |
|---|---|
| AI sentiment, more news sources for prediction | Tested: news describes moves after they happen; it does not predict |
| Email delivery of the brief | Gmail would need full mailbox access; Microsoft 365 can't send. Phone notification and saved documents instead |
| Anthropic API key for the brief | Claude Code and the routine run on the existing Claude plan, at no extra charge |
| Holiday-aware "prices are old" warning | A harmless false alarm after a long weekend isn't worth yearly upkeep |
| PSX data portal | Its terms restrict reuse; Sarmaaya provides the same data |
| All 30 KMI-30 stocks as instruments | Would double the app's size and load; ranked on the Rankings page instead |
| Indicators needing high/low prices (ATR, ADX...) | The sources give closing prices only |

## How the pieces fit
```
Sarmaaya + Google News ──► Worker (every 6 h) ──► PostgreSQL ──► API ──► website (localhost:3000)
                                                               └──► Brief page ──► Claude Code on this PC
Sarmaaya + Google News ──► skill scripts/facts.py ──► morning routine (cloud) ──► Claude document + phone
                                   └── same writing rules as the Brief page (test keeps them equal)
```
