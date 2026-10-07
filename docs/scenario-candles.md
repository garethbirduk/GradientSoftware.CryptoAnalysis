# Scenario candles: interpreting a predicted path

Discussion summary, 2026-10-03. Nothing here is built. This records the idea,
the feasibility view, and the open questions.

## The idea

The tour is retrospective: it replays real candles and highlights MSB, BOS,
Ranges, Trends and so on. The new idea looks forward.

1. The user draws a predicted price path (a "squiggle") from the current candle
   into the future, as charting tools already allow.
2. The path is turned into virtual candles.
3. The engine analyses real plus virtual candles together, so the predicted
   stretch is interpreted, not just drawn.

Use case: a Range has been spotted and about 10 candles of it exist, but it has
not ended. Suppose price follows this path for the next 20 candles. The Range
box keeps drawing along the path, ends if the path exits it, and the page
reports the number of tradable options for a given strategy.

## Feasibility

Assumption, not yet checked against the code: the analysis runs as a function
over a candle list, as the replay suggests.

1. If the engine only sees candles, virtual candles appended to the real ones
   get Ranges, MSB, BOS, Trends and strategy entries with no engine changes.
2. The only new state is a "virtual" flag per candle, used to draw them ghosted
   and to keep their trades out of real backtest results.
3. A squiggle is a few turning points joined by lines, which is already a
   sawtooth. Each turning point is a swing at the level being drawn.
4. Clicked anchor points (candle index, price) are simpler than freehand
   drawing, are repeatable, and could be stored as a tour scene.

## The hard part: candle synthesis

5. The path gives opens and closes. Wicks are not in the squiggle and have to
   be invented.
6. Options: body-only candles, wicks sized from the recent real candles'
   average, or seeded random wicks.
7. Anything finer than the squiggle's own level is an artefact of the
   synthesis. Swings at the drawn level are trustworthy; lower-level swings
   come from added noise. Results should be reported at the drawn level and
   coarser only.

## The Range case

8. A Range ends on a close beyond the band, and closes are what the squiggle
   defines directly. The extension and the end of the box do not depend on
   invented wicks.
9. The end lands on the first virtual candle whose close is beyond the band.
10. A path that never exits leaves the box open, the same as a live Range.
11. To check in the code: whether any zone condition triggers on a wick. If so,
    that part of the entry count does depend on wick synthesis.

## Design questions

12. Drawing: solid over real candles, ghosted over virtual ones, including the
    end marker, so it is obvious which part has happened.
13. Retroactive effects: virtual candles can confirm things in the real region
    (an interim swing becoming confirmed, an MSB completing). These need
    marking as conditional on the scenario. This is the one point needing real
    design thought.
14. Brittleness: an entry count from one squiggle changes if one close moves
    slightly across a band. Mitigation: run many seeded variants of the same
    path and report a spread, not a single count.
15. False confidence: ghost candles look like data. The display must make
    clear they are a scenario, not a prediction.
16. Open: is this mainly for the tour (scripted scenarios, no drawing UI
    needed) or for live use on the current chart?

## Usefulness

17. Teaching: lets the tour say "if price does this next, here is what the
    strategy does".
18. Strategy testing: hand-drawn paths become a way to author edge cases (a
    tie, a wick beyond the band with a close back inside, a late Range end)
    without searching history for them.
19. Path dependence: static levels can say where a Range ends, but only a path
    shows how many zone entries occur before that.

Suggested first version: anchor points, body-only candles, the virtual flag,
results at the drawn level and coarser. Wick synthesis and the multi-variant
spread second.

## Is it novel?

From memory, not from a search, so "likely" and not proven.

20. Tools that draw a future without interpreting it: TradingView's Ghost Feed
    (fake candles along a clicked path, the closest visual match), Bars
    Pattern, and the path, forecast and projection drawing tools.
21. Tools that interpret hypothetical data that is not a drawn path: Monte
    Carlo and synthetic-data features in backtesting packages, options what-if
    analysis (a single price and date, no path), bar replay (historical).
22. The idea joins the two: a human-authored path run through the real engine,
    with structure interpreted and not only profit.
23. A likely reason it is rare is the wick problem. This model's reliance on
    closes makes the core of it well defined.

## How the wider auto-analysis compares

Also from memory.

24. Existing: TradingView "Smart Money Concepts" scripts (LuxAlgo and clones)
    marking BOS, change of character, order blocks and zones; the
    smartmoneyconcepts Python package; TrendSpider, Autochartist and Trading
    Central for trendlines and patterns; MotiveWave and WaveBasis for Elliott
    wave counts.
25. Where they typically stop: swings from a fixed lookback, one or two levels,
    unspecified edge cases (ties, close versus wick, unconfirmed swings),
    repainting, marks for the eye and not for backtesting, no explanation of
    why a mark appeared.
26. Less common here: a sawtooth per level with defined rules; Trends, Ranges
    and Retracements built from that one model; the same terms feeding
    strategy conditions and backtesting; the tour explaining the engine's own
    reading.
27. Caveats: rigour is not the same as edge, which only backtesting can show.
    "Uncommon" means among tools a retail trader can get.

## Commercialisation

28. Routes: an education product (tour videos and interactive explainer); a
    hosted web app on subscription; a TradingView indicator (needs a Pine
    Script port); alerts or signals; licensing to trading educators.
29. In favour: explainability, the scenario tool, one model driving analysis,
    strategy and teaching.
30. Against: a crowded market that responds to promised profit; no backtest
    evidence of an edge yet; regulation of signals and anything resembling
    advice (needs proper legal advice); exchange terms on redistributing
    candle data; the support load of a solo product.
31. To check first: if the White Belt system and its terms come from someone
    else's course, selling a tool built on them may need permission, or could
    be a partnership.
32. Suggested approach: test demand cheaply by publishing a few tour videos.
    If that draws interest, start with the education or licensing route, and
    consider the hosted app once there are paying users.
