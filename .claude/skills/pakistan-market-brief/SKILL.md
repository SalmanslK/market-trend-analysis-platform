---
name: pakistan-market-brief
description: Writes a plain-words market brief for a Pakistani investor in Meezan (Islamic) funds, gold and cash, the same brief the Market Analytics app writes. It gathers fresh figures itself (PSX indices, Meezan funds, gold, USD/PKR, ten Shariah stocks, trading volume, headlines, and what history says followed similar trends) and explains them for a non-expert, including the outlook for the coming weeks and the next year. Use it whenever the user asks for a market brief, daily brief, market update or summary of Pakistan's markets, how the KSE-100, KMI-30, Meezan funds, gold or the rupee are doing, what might happen next, or what is going on in the markets today, even if they don't say "brief".
---

# Pakistan market brief

The reader is one person in Pakistan with no finance background who holds or is considering Meezan funds, gold and
cash. They want to understand what is happening and what might come next, so they can make their own decisions. The
brief must match what their Market Analytics app shows: same figures, same honesty rules, same sections. That is why
the figures come from a script rather than from memory or a web search, and why the writing rules are a separate
file shared with the app (a test in the app keeps the two identical).

## 1. Gather the facts

Run the bundled script from this skill's folder (it needs only Python 3's standard library and internet access to
beta-restapi.sarmaaya.pk and news.google.com):

```bash
python3 scripts/facts.py > /tmp/facts.json    # use "python" where "python3" doesn't exist
```

It prints one JSON document: each instrument's latest price, changes (day to year), 52-week range, worst fall and
long-term trend state; trading volume against its usual level; the "stronger than cash" check; the week's headlines;
the app's research findings; and `whatFollowedSimilarTrends`, the historical base rates the outlook must be built on.
It takes a minute or two (it reads 20 years of prices).

If it fails, read the error. A "403 Forbidden" or "Tunnel connection failed" means the environment blocks those two
websites: say so plainly, tell the user to allow beta-restapi.sarmaaya.pk and news.google.com in the environment's
network settings, and stop. Do not write a brief from memory or from web searches instead, because the user relies
on its figures matching their app.

## 2. Write the brief

Read `references/writing-rules.md` and follow it exactly: it is the app's own instructions (the persona, what may be
said about the future, no buy/sell instructions, the fixed sections, the Markdown subset, sources at the end). Use
the facts file as the facts block it refers to. You may search the web a few times for world markets and Pakistan's
economy (inflation, the policy rate, reserves, the IMF, oil), as the rules allow.

Two things differ from the app, because the facts are gathered without the app's database:
- There is no news-mood score per instrument. Read the headlines in the facts instead, and describe their tone in
  words if it helps.
- The history-based outlook covers the KSE-100, KMI-30, Meezan Gold Fund and USD/PKR. For Meezan Islamic Fund, use
  the KMI-30's figures, since the fund holds mostly KMI-30 companies, and say so.

Open with a line naming the date the facts were written (`writtenOn`) and the date of the latest prices, so the reader
knows how fresh the figures are (fund prices are often a day or two behind the index).

## 3. Deliver it

Show the complete brief in your reply. If the user asked for a file, also save it as `market-brief-<date>.md`.
If the user asked for it by email and you have a tool that can send email, send it only to the user's own address,
never anyone else's.

## Why these rules

The user's own testing found that technical signals, news mood and trading volume describe the past but did not
predict it, and that only the "stronger than cash" rule held up. A brief that sounded confident about next month
would mislead them. The historical base rates (how often similar past situations ended higher, the typical result
and the range) are the honest way to talk about the future, alongside a plain statement that nobody can reliably
predict markets over weeks or months.
