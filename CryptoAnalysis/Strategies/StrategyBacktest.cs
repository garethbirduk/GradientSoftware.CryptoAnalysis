using System.Globalization;
using System.Text;
using Gradient.CryptoAnalysis.Conditions.PriceConditions;
using Gradient.CryptoAnalysis.Research;

namespace Gradient.CryptoAnalysis.Strategies;

public enum EnumTradeOutcome
{
    TakeProfit,
    StopLoss,

    /// <summary>
    /// Still open at the last candle, valued at its close.
    /// </summary>
    Open,
}

/// <summary>
/// A candle that met a step of an entry condition, and the first candle the step read to it, From: the first of a run, or
/// where the Swing or Trend a break is read from begins.
/// </summary>
public sealed record StrategyStep(int Index, DateTime Time, int From);

/// <summary>
/// One trade of a backtest. EntryPrice and ExitPrice are the market's, where the orders were: the entry candle's close, and
/// the target or the open the trade left at. Profit is in price units per unit traded, signed the trade's way (a Short
/// gains as the price falls), less its Fees and Slippage, each a cost in price units; ProfitPercent is that as a percentage
/// of the entry price. BothTouched marks a candle that reached the take profit and the stop loss alike, which is counted as
/// the stop loss since the candle does not say which came first. Met is the candles that met the steps of the strategy's
/// After, in order; empty for an entry of one step. EntryFrom is the first candle the step that entered read. Range is the
/// Range the entry condition held, when it has a Range step.
/// </summary>
public sealed record StrategyTrade(int Number, EnumTradeDirection Direction, int EntryIndex, DateTime EntryTime, double EntryPrice,
    double TakeProfit, double StopLoss, int ExitIndex, DateTime ExitTime, double ExitPrice, EnumTradeOutcome Outcome,
    double Profit, double ProfitPercent, int Candles, bool BothTouched, IReadOnlyList<StrategyStep> Met, double Fees = 0, double Slippage = 0)
{
    public int EntryFrom { get; init; } = EntryIndex;

    public HeldRange? Range { get; init; }

    /// <summary>
    /// The trade's target in R as it was entered: the Take Profit's distance from the entry over the Stop Loss's.
    /// </summary>
    public double R => Math.Abs(TakeProfit - EntryPrice) / Math.Abs(EntryPrice - StopLoss);

    /// <summary>
    /// The first candle the entry condition read: the earliest any of its steps read from.
    /// </summary>
    public int FirstRead => Met.Select(x => x.From).Append(EntryFrom).Min();
}

/// <summary>
/// The totals of a backtest, over the trades that closed; trades still open at the end are only counted in StillOpen. The
/// profits are after fees and slippage, which Fees and Slippage total.
/// </summary>
public sealed record BacktestSummary(int Trades, int Won, int Lost, int StillOpen, double WinRate, double TotalProfit,
    double TotalProfitPercent, double AverageWin, double AverageLoss, double LargestWin, double LargestLoss, double? ProfitFactor,
    int BothTouched, double Fees = 0, double Slippage = 0);

/// <summary>
/// A strategy run over a dataset: what was run, on what, and every trade it made.
/// </summary>
public sealed record BacktestRun(Strategy Strategy, string Dataset, DateTime From, DateTime To, int Candles, DateTime Run,
    BacktestSummary Summary, List<StrategyTrade> Trades);

/// <summary>
/// Runs a strategy over prices candle by candle, seeing only the candles up to each one. A trade is entered at the close of
/// the candle that calls for it, with its targets set there, and is watched from the next candle on: a candle that opens
/// beyond a target fills at its open, otherwise one whose high or low reaches a target fills at the target. Each trade's
/// profit is after the strategy's fees and slippage (see <see cref="Strategy.FeePercent"/>, <see cref="Strategy.SlippagePercent"/>).
/// </summary>
public static class StrategyBacktest
{
    /// <summary>
    /// Runs the strategy over the prices and returns its trades with their totals. With a window, trades are entered only at
    /// the candles in it, first up to but not including end, though the strategy reads the candles before it and a trade can
    /// close after it; the run's dates and candles are the window's.
    /// </summary>
    public static BacktestRun Run(IReadOnlyList<Price> prices, Strategy strategy, string dataset = "", (int First, int End)? window = null)
    {
        var errors = strategy.Validate();
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(strategy));

        var (first, end) = window ?? (0, prices.Count);
        var trades = Trades(prices, strategy, Signals(prices, strategy).Where(x => x.Index >= first && x.Index < end));
        return new BacktestRun(strategy, dataset, end > first ? prices[first].DateTime : default, end > first ? prices[end - 1].DateTime : default,
            end - first, DateTime.UtcNow, Summarise(trades), trades);
    }

    /// <summary>
    /// The candles of the prices from a date up to but not including another, as a window for <see cref="Run"/>: all of
    /// them when neither is given.
    /// </summary>
    public static (int First, int End) Window(IReadOnlyList<Price> prices, DateTime? from, DateTime? to)
    {
        var first = from is { } f ? FirstAtOrAfter(prices, f) : 0;
        var end = to is { } t ? FirstAtOrAfter(prices, t) : prices.Count;
        return (first, Math.Max(first, end));
    }

    // The first candle at or after the time; the number of candles when none is.
    private static int FirstAtOrAfter(IReadOnlyList<Price> prices, DateTime time)
    {
        var (lo, hi) = (0, prices.Count);
        while (lo < hi)
        {
            var mid = (lo + hi) / 2;
            if (prices[mid].DateTime < time)
                lo = mid + 1;
            else
                hi = mid;
        }

        return lo;
    }

    /// <summary>
    /// The trades the strategy's exits make from the given entry candles, in order, each with the candles that met the steps
    /// before it: one at each that its targets can be set at, except while another is open when OnePositionAtATime is set.
    /// </summary>
    public static List<StrategyTrade> Trades(IReadOnlyList<Price> prices, Strategy strategy, IEnumerable<(int Index, IReadOnlyList<int> Met)> signals)
    {
        var trades = new List<StrategyTrade>();
        var steps = strategy.Steps;
        var free = 0;
        foreach (var (i, met) in signals)
        {
            if (strategy.OnePositionAtATime && i < free)
                continue;

            var trade = Trade(prices, i, strategy, trades.Count + 1, met.Select((x, k) => new StrategyStep(x, prices[x].DateTime, ReadFrom(prices, x, steps[k]))).ToList(),
                RangeOf(prices, strategy, i, met));
            if (trade == null)
                continue;
            trades.Add(trade);
            free = trade.Outcome == EnumTradeOutcome.Open ? prices.Count : trade.ExitIndex;
        }

        return trades;
    }

    /// <summary>
    /// The candles at whose close the strategy's entry condition is met, read candle by candle from the candles up to each
    /// alone, each with the candles that met the steps before the last. A step counts only when the first candle it reads
    /// comes after the candle that met the step before, and within the step's Within of it; the latest such is the one
    /// taken. Once the last step is met the chain starts again, whether or not a trade can be entered there.
    /// A step's kills are looked for at each close after the step is met, until the trade is entered: one met there ends the
    /// wait, so the candles that met that step and those after it are forgotten, before the candle is read for the steps.
    /// A Range's end forgets those that hold the Range it ends. A chain holds the Range of its Range step, so a step after
    /// one follows the latest candle of the step before for each Range held, should one of them end.
    /// </summary>
    public static List<(int Index, IReadOnlyList<int> Met)> Signals(IReadOnlyList<Price> prices, Strategy strategy)
    {
        var steps = strategy.Steps;
        var killed = steps.Any(x => x.Kill is { Count: > 0 });
        // The candles that have met each step since the chain last started, each with the candles of the steps before it.
        var met = steps.Select(_ => new List<Arming>()).ToArray();
        var signals = new List<(int, IReadOnlyList<int>)>();
        for (var i = 0; i < prices.Count; i++)
        {
            if (killed)
                Kill(prices, i, steps, met);

            // The last step first, so a candle is not taken as the step after one it has itself just met.
            for (var k = steps.Count - 1; k >= 0; k--)
            {
                var step = steps[k];
                if (!Enters(prices, i, step))
                    continue;
                var held = Held(prices, i, step);
                List<Arming> made = k == 0 ? [new Arming(i, [i], held)]
                    : Follows(met[k - 1], i - step.Reads, i, step.Within).Select(x => new Arming(i, [.. x.Chain, i], held ?? x.Range)).ToList();
                if (made.Count == 0)
                    continue;
                if (k < steps.Count - 1)
                {
                    // A Range step holds its own Range, whichever chain it follows.
                    met[k].AddRange(held != null ? made.Take(1) : made);
                    continue;
                }

                signals.Add((i, made[0].Chain[..^1]));
                foreach (var m in met)
                    m.Clear();
                break;
            }
        }

        return signals;
    }

    // A candle that met a step, with the candles of the steps before it and the Range the chain holds.
    private sealed record Arming(int At, int[] Chain, HeldRange? Range);

    // Forgets what a kill met at the close of candle index ends: the candles that met its step, and those of the steps after.
    private static void Kill(IReadOnlyList<Price> prices, int index, IReadOnlyList<StrategyEntry> steps, List<Arming>[] met)
    {
        for (var k = 0; k < steps.Count - 1; k++)
        {
            foreach (var kill in steps[k].Kill ?? [])
            {
                Predicate<Arming>? ended = kill.Type == StrategyEntry.RangeEnd ? x => x.Range?.EndedBy(prices[index].Close) == true
                    : Enters(prices, index, kill) ? _ => true : null;
                if (ended == null)
                    continue;
                for (var j = k; j < steps.Count - 1; j++)
                    met[j].RemoveAll(ended);
            }
        }
    }

    // The latest candle that met the step before, ahead of the first candle a step reads and within its reach, first; then,
    // when the chains hold Ranges, the latest for each other Range held.
    private static List<Arming> Follows(List<Arming> before, int first, int at, int? within)
    {
        var found = new List<Arming>();
        for (var j = before.Count - 1; j >= 0; j--)
        {
            var arming = before[j];
            if (within != null && at - arming.At > within)
                break;
            if (arming.At >= first || found.Any(x => SameLines(x.Range!, arming.Range!)))
                continue;
            found.Add(arming);
            if (arming.Range == null)
                break;
        }

        return found;
    }

    private static bool SameLines(HeldRange a, HeldRange b) =>
        (a.Low, a.High, a.BandLow, a.BandHigh, a.BuyLowTop, a.SellHighBottom) == (b.Low, b.High, b.BandLow, b.BandHigh, b.BuyLowTop, b.SellHighBottom);

    /// <summary>
    /// Whether a step is met at the close of candle index.
    /// </summary>
    public static bool Enters(IReadOnlyList<Price> prices, int index, StrategyEntry entry)
    {
        if (entry.IsStructure)
            return Found(prices, index, entry) != null;
        if (entry.IsRange)
            return Held(prices, index, entry) != null;
        if (entry.Type != StrategyEntry.SuccessiveCandles)
            return false;

        // A run of Length candles ending here that the candle before it does not belong to, so a longer run enters once.
        var first = index - entry.Length + 1;
        if (first < 0)
            return false;
        for (var i = first; i <= index; i++)
        {
            if (Colour(prices[i]) != entry.Colour)
                return false;
        }

        return first == 0 || Colour(prices[first - 1]) != entry.Colour;
    }

    /// <summary>
    /// The break that meets a market structure step at the close of candle index, with what it was read from; null when
    /// none does, or the step is not one of market structure.
    /// </summary>
    public static KnownBreak? Found(IReadOnlyList<Price> prices, int index, StrategyEntry entry)
    {
        if (!entry.IsStructure)
            return null;
        var kind = entry.Type == StrategyEntry.MarketStructureBreak ? EnumKnownBreak.MarketStructureBreak : EnumKnownBreak.BreakOfStructure;
        return KnownStructure.Breaks(prices)[index].FirstOrDefault(x => x.Level == entry.Level && x.Kind == kind && x.Direction == entry.Direction
            && entry.Type switch
            {
                StrategyEntry.Swing => entry.Strength == EnumSwingStrength.Any || x.Weak == (entry.Strength == EnumSwingStrength.Weak),
                StrategyEntry.Trend => x.Run == entry.Swings,
                _ => true,
            });
    }

    /// <summary>
    /// The Range a Range step holds when it is met at the close of candle index: the one holding at the step's Level
    /// there, as a chart of the last candles to it shows (see <see cref="KnownRanges"/>), when the candle reaches into the
    /// step's zone of it, for a RangeZone, or sweeps it, for a RangeSweep: a wick beyond its low or high and a close back
    /// at or inside it. Null when the step is not met there, or is not a Range step.
    /// </summary>
    public static HeldRange? Held(IReadOnlyList<Price> prices, int index, StrategyEntry entry)
    {
        if (!entry.IsRange || KnownRanges.Holding(prices, entry.Retracement, entry.Band)[index].FirstOrDefault(x => x.Level == entry.Level) is not { } range)
            return null;
        var (top, bottom) = (range.BuyLowTop(entry.Depth), range.SellHighBottom(entry.Depth));
        var candle = prices[index];
        var reached = entry.Type == StrategyEntry.RangeSweep
            ? entry.Zone == EnumRangeZone.BuyLow ? candle.Low < range.Low && candle.Close >= range.Low : candle.High > range.High && candle.Close <= range.High
            : entry.Zone == EnumRangeZone.BuyLow ? candle.Low <= top : candle.High >= bottom;
        return reached ? new HeldRange(range.Level, range.Direction, range.Low, range.High, range.BandLow, range.BandHigh, top, bottom, range.Retracement, range.From, range.Identified, index) : null;
    }

    /// <summary>
    /// The Range the entry condition held for a trade entered at candle index after the candles that met its steps before the
    /// last: that of its last RangeZone step, at the candle that met it. Null when it has none.
    /// </summary>
    public static HeldRange? RangeOf(IReadOnlyList<Price> prices, Strategy strategy, int index, IReadOnlyList<int> met)
    {
        var steps = strategy.Steps;
        for (var k = steps.Count - 1; k >= 0; k--)
        {
            if (steps[k].IsRange)
                return k < met.Count ? Held(prices, met[k], steps[k]) : k == steps.Count - 1 ? Held(prices, index, steps[k]) : null;
        }

        return null;
    }

    /// <summary>
    /// The first candle a step met at the close of candle index read: the first of its run, or where what its break is read
    /// from begins, the Swing, or for a Trend the first of its Swings; for a Range's zone, where the Range's Retracement is
    /// measured from.
    /// </summary>
    public static int ReadFrom(IReadOnlyList<Price> prices, int index, StrategyEntry entry) =>
        Held(prices, index, entry) is { } held ? held.From
        : Found(prices, index, entry) is not { } found ? Math.Max(0, index - entry.Reads) : entry.Type == StrategyEntry.Trend ? found.Begins : found.From;

    /// <summary>
    /// Every candle that meets the strategy's entry condition, found another way from Signals, to check the trades against:
    /// each step's candles as <see cref="Occurrences(List{Price}, StrategyEntry)"/> finds them, then each candle of the last
    /// step that a chain of the steps before it can be traced back from, all after the candle the chain was last met at. A
    /// chain lasts until the first kill after any of its candles, so it must last beyond the candle it is traced from.
    /// </summary>
    public static List<int> Occurrences(List<Price> prices, Strategy strategy)
    {
        var steps = strategy.Steps;
        var each = steps.Select(x => Occurrences(prices, x)).ToList();
        // The candles each step's kills are met at, but for a Range's end, which is of the Range each candle holds.
        var kills = steps.Select(x => (x.Kill ?? []).Where(k => k.Type != StrategyEntry.RangeEnd).SelectMany(k => Occurrences(prices, k)).Distinct().Order().ToList()).ToList();
        var found = new List<int>();
        var after = -1;
        var lasts = new Dictionary<(int, int), int>();
        var killedAt = new Dictionary<(int, int), int>();
        foreach (var at in each[^1])
        {
            if (Lasts(steps.Count - 1, at) <= at)
                continue;
            found.Add(at);
            after = at;
            lasts.Clear();
        }

        return found;

        // The candle a candle that met step k is killed at, as a chain of one: the first after it that meets a kill of the step.
        int Killed(int k, int at)
        {
            if (killedAt.TryGetValue((k, at), out var known))
                return known;
            var next = kills[k].BinarySearch(at + 1);
            var killed = (next < 0 ? ~next : next) is var j && j < kills[k].Count ? kills[k][j] : int.MaxValue;
            if (steps[k].Kill?.Any(x => x.Type == StrategyEntry.RangeEnd) == true && Held(prices, at, steps[k]) is { } held)
            {
                for (var i = at + 1; i < Math.Min(killed, prices.Count); i++)
                {
                    if (held.EndedBy(prices[i].Close))
                    {
                        killed = i;
                        break;
                    }
                }
            }

            return killedAt[(k, at)] = killed;
        }

        // How long the longest-lasting chain to a candle that meets step k lasts: the candle its first kill is met at, the
        // candle itself when no chain of the steps before k can be traced back from it, or no candle at all when none kills it.
        int Lasts(int k, int at)
        {
            var step = steps[k];
            if (k == 0)
                return k == steps.Count - 1 ? int.MaxValue : Killed(k, at);
            if (lasts.TryGetValue((k, at), out var known))
                return known;
            var own = k == steps.Count - 1 ? int.MaxValue : Killed(k, at);
            var before = at;
            foreach (var x in each[k - 1].Where(x => x > after && x < at - step.Reads && (step.Within == null || at - x <= step.Within)))
            {
                before = Math.Max(before, Lasts(k - 1, x));
                if (before == int.MaxValue)
                    break;
            }

            return lasts[(k, at)] = Math.Min(own, before);
        }
    }

    /// <summary>
    /// Every candle that meets a step, found another way from Enters: for SuccessiveCandles, the Length-th candle of each
    /// run of at least Length candles of the colour, from CandleRuns. A market structure step has no other way: its candles
    /// are those KnownStructure has its break at, and a Range's zone those KnownRanges has the Range holding at.
    /// </summary>
    public static List<int> Occurrences(List<Price> prices, StrategyEntry entry)
    {
        if (entry.IsStructure)
            return Enumerable.Range(0, prices.Count).Where(i => Found(prices, i, entry) != null).ToList();
        if (entry.IsRange)
            return Enumerable.Range(0, prices.Count).Where(i => Held(prices, i, entry) != null).ToList();
        if (entry.Type != StrategyEntry.SuccessiveCandles)
            return [];

        var index = prices.Select((p, i) => (p.DateTime, i)).ToDictionary(x => x.DateTime, x => x.i);
        return CandleRuns.Runs(prices, entry.Length)
            .Where(r => r.Green == (entry.Colour == EnumCandleColour.Green))
            .Select(r => index[r.Start.Time] + entry.Length - 1)
            .ToList();
    }

    /// <summary>
    /// Whether a trade entered at the close of candle index can have both its targets set there (see
    /// <see cref="StrategyTarget.Price"/>); a candle where one cannot is passed over. A target on a Range's lines is set
    /// from the Range the entry condition held, which the candles that met its steps before the last give.
    /// </summary>
    public static bool Measurable(IReadOnlyList<Price> prices, int index, Strategy strategy, IReadOnlyList<int>? met = null)
    {
        var range = RangeOf(prices, strategy, index, met ?? []);
        return strategy.TakeProfit.Price(prices, index, strategy.Direction, profit: true, range: range) is double takeProfit
            && strategy.StopLoss.Price(prices, index, strategy.Direction, profit: false, takeProfit, range) != null;
    }

    /// <summary>
    /// The totals of a list of trades.
    /// </summary>
    public static BacktestSummary Summarise(IReadOnlyList<StrategyTrade> trades)
    {
        var closed = trades.Where(x => x.Outcome != EnumTradeOutcome.Open).ToList();
        var wins = closed.Where(x => x.Profit > 0).ToList();
        var losses = closed.Where(x => x.Profit < 0).ToList();
        var grossLoss = -losses.Sum(x => x.Profit);
        return new BacktestSummary(
            closed.Count,
            wins.Count,
            losses.Count,
            trades.Count - closed.Count,
            closed.Count > 0 ? 100.0 * wins.Count / closed.Count : 0,
            closed.Sum(x => x.Profit),
            closed.Sum(x => x.ProfitPercent),
            wins.Count > 0 ? wins.Average(x => x.Profit) : 0,
            losses.Count > 0 ? losses.Average(x => x.Profit) : 0,
            wins.Count > 0 ? wins.Max(x => x.Profit) : 0,
            losses.Count > 0 ? losses.Min(x => x.Profit) : 0,
            grossLoss > 0 ? wins.Sum(x => x.Profit) / grossLoss : null,
            closed.Count(x => x.BothTouched),
            closed.Sum(x => x.Fees),
            closed.Sum(x => x.Slippage));
    }

    /// <summary>
    /// The trades as CSV, a row each, for spreadsheets and charts.
    /// </summary>
    public static string ToCsv(IEnumerable<StrategyTrade> trades)
    {
        var csv = new StringBuilder("Number,Direction,EntryTime,EntryPrice,TakeProfit,StopLoss,ExitTime,ExitPrice,Outcome,Profit,ProfitPercent,Candles,BothTouched,Fees,Slippage,R\n");
        foreach (var t in trades)
        {
            csv.AppendLine(string.Join(",", t.Number, t.Direction, Time(t.EntryTime), Number(t.EntryPrice), Number(t.TakeProfit), Number(t.StopLoss),
                Time(t.ExitTime), Number(t.ExitPrice), t.Outcome, Number(t.Profit), Number(t.ProfitPercent), t.Candles, t.BothTouched,
                Number(t.Fees), Number(t.Slippage), Number(t.R)));
        }

        return csv.ToString();
    }

    private static string Time(DateTime time) => time.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);

    private static StrategyTrade? Trade(IReadOnlyList<Price> prices, int index, Strategy strategy, int number, IReadOnlyList<StrategyStep> met, HeldRange? range)
    {
        var entry = prices[index].Close;
        if (strategy.TakeProfit.Price(prices, index, strategy.Direction, profit: true, range: range) is not double takeProfit
            || strategy.StopLoss.Price(prices, index, strategy.Direction, profit: false, takeProfit, range) is not double stopLoss)
            return null;

        var sign = strategy.Direction == EnumTradeDirection.Long ? 1 : -1;
        // Prices above the entry are "beyond" for a Long's take profit and a Short's stop loss, and the other way round.
        bool Reaches(double price, double target, bool profit) => (sign > 0) == profit ? price >= target : price <= target;

        for (var j = index + 1; j < prices.Count; j++)
        {
            var p = prices[j];
            double? exit = null;
            var outcome = EnumTradeOutcome.StopLoss;
            var both = false;
            if (Reaches(p.Open, stopLoss, profit: false))
                exit = p.Open;
            else if (Reaches(p.Open, takeProfit, profit: true))
                (exit, outcome) = (p.Open, EnumTradeOutcome.TakeProfit);
            else
            {
                var hitLoss = Reaches(sign > 0 ? p.Low : p.High, stopLoss, profit: false);
                var hitProfit = Reaches(sign > 0 ? p.High : p.Low, takeProfit, profit: true);
                both = hitLoss && hitProfit;
                if (hitLoss)
                    exit = stopLoss;
                else if (hitProfit)
                    (exit, outcome) = (takeProfit, EnumTradeOutcome.TakeProfit);
            }

            if (exit is double price)
                return Made(j, price, outcome, both);
        }

        return Made(prices.Count - 1, prices[^1].Close, EnumTradeOutcome.Open, false);

        // The costs: a fee on each side, and slippage on the market orders, the entry and a Stop Loss. A trade still open at
        // the end is valued as if closed at the last close, paying the fee to close but not slipping.
        StrategyTrade Made(int exitIndex, double exitPrice, EnumTradeOutcome outcome, bool both)
        {
            var fees = (strategy.CountFees ? strategy.FeePercent : 0) / 100 * (entry + exitPrice);
            var slippage = (strategy.CountSlippage ? strategy.SlippagePercent : 0) / 100 * (entry + (outcome == EnumTradeOutcome.StopLoss ? exitPrice : 0));
            var profit = sign * (exitPrice - entry) - fees - slippage;
            return new StrategyTrade(number, strategy.Direction, index, prices[index].DateTime, entry, takeProfit, stopLoss,
                exitIndex, prices[exitIndex].DateTime, exitPrice, outcome, profit, 100 * profit / entry, exitIndex - index, both, met, fees, slippage)
            {
                EntryFrom = ReadFrom(prices, index, strategy.Entry),
                Range = range,
            };
        }
    }

    private static EnumCandleColour? Colour(Price price) =>
        price.Close > price.Open ? EnumCandleColour.Green : price.Close < price.Open ? EnumCandleColour.Red : null;
}
