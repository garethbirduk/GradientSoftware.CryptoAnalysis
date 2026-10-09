using System.Globalization;
using System.Text;

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
/// A candle that met a step of an entry condition.
/// </summary>
public sealed record StrategyStep(int Index, DateTime Time);

/// <summary>
/// One trade of a backtest. Profit is in price units per unit traded, signed the trade's way (a Short gains as the price
/// falls); ProfitPercent is that as a percentage of the entry price. BothTouched marks a candle that reached the take
/// profit and the stop loss alike, which is counted as the stop loss since the candle does not say which came first. Met is
/// the candles that met the steps of the strategy's After, in order; empty for an entry of one step.
/// </summary>
public sealed record StrategyTrade(int Number, EnumTradeDirection Direction, int EntryIndex, DateTime EntryTime, double EntryPrice,
    double TakeProfit, double StopLoss, int ExitIndex, DateTime ExitTime, double ExitPrice, EnumTradeOutcome Outcome,
    double Profit, double ProfitPercent, int Candles, bool BothTouched, IReadOnlyList<StrategyStep> Met)
{
    /// <summary>
    /// The first candle the entry condition read: the first its first step read.
    /// </summary>
    public int FirstRead(Strategy strategy) => Math.Max(0, (Met.Count > 0 ? Met[0].Index : EntryIndex) - strategy.Steps[0].Reads);
}

/// <summary>
/// The totals of a backtest, over the trades that closed; trades still open at the end are only counted in StillOpen.
/// </summary>
public sealed record BacktestSummary(int Trades, int Won, int Lost, int StillOpen, double WinRate, double TotalProfit,
    double TotalProfitPercent, double AverageWin, double AverageLoss, double LargestWin, double LargestLoss, double? ProfitFactor,
    int BothTouched);

/// <summary>
/// A strategy run over a dataset: what was run, on what, and every trade it made.
/// </summary>
public sealed record BacktestRun(Strategy Strategy, string Dataset, DateTime From, DateTime To, int Candles, DateTime Run,
    BacktestSummary Summary, List<StrategyTrade> Trades);

/// <summary>
/// Runs a strategy over prices candle by candle, seeing only the candles up to each one. A trade is entered at the close of
/// the candle that calls for it, with its targets set there, and is watched from the next candle on: a candle that opens
/// beyond a target fills at its open, otherwise one whose high or low reaches a target fills at the target.
/// </summary>
public static class StrategyBacktest
{
    /// <summary>
    /// Runs the strategy over the prices and returns its trades with their totals.
    /// </summary>
    public static BacktestRun Run(IReadOnlyList<Price> prices, Strategy strategy, string dataset = "")
    {
        var errors = strategy.Validate();
        if (errors.Count > 0)
            throw new ArgumentException(string.Join(" ", errors), nameof(strategy));

        var trades = Trades(prices, strategy, Signals(prices, strategy));
        return new BacktestRun(strategy, dataset, prices.Count > 0 ? prices[0].DateTime : default, prices.Count > 0 ? prices[^1].DateTime : default,
            prices.Count, DateTime.UtcNow, Summarise(trades), trades);
    }

    /// <summary>
    /// The trades the strategy's exits make from the given entry candles, in order, each with the candles that met the steps
    /// before it: one at each that its targets can be set at, except while another is open when OnePositionAtATime is set.
    /// </summary>
    public static List<StrategyTrade> Trades(IReadOnlyList<Price> prices, Strategy strategy, IEnumerable<(int Index, IReadOnlyList<int> Met)> signals)
    {
        var trades = new List<StrategyTrade>();
        var free = 0;
        foreach (var (i, met) in signals)
        {
            if (strategy.OnePositionAtATime && i < free)
                continue;

            var trade = Trade(prices, i, strategy, trades.Count + 1, met.Select(x => new StrategyStep(x, prices[x].DateTime)).ToList());
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
    /// </summary>
    public static List<(int Index, IReadOnlyList<int> Met)> Signals(IReadOnlyList<Price> prices, Strategy strategy)
    {
        var steps = strategy.Steps;
        // The candles that have met each step since the chain last started, each with the candles of the steps before it.
        var met = steps.Select(_ => new List<(int At, int[] Chain)>()).ToArray();
        var signals = new List<(int, IReadOnlyList<int>)>();
        for (var i = 0; i < prices.Count; i++)
        {
            // The last step first, so a candle is not taken as the step after one it has itself just met.
            for (var k = steps.Count - 1; k >= 0; k--)
            {
                var step = steps[k];
                if (!Enters(prices, i, step))
                    continue;
                int[]? chain = k == 0 ? [i] : Follows(met[k - 1], i - step.Reads, i, step.Within) is { } before ? [.. before, i] : null;
                if (chain == null)
                    continue;
                if (k < steps.Count - 1)
                {
                    met[k].Add((i, chain));
                    continue;
                }

                signals.Add((i, chain[..^1]));
                foreach (var m in met)
                    m.Clear();
                break;
            }
        }

        return signals;
    }

    // The chain of the latest candle that met the step before, ahead of the first candle a step reads and within its reach.
    private static int[]? Follows(List<(int At, int[] Chain)> before, int first, int at, int? within)
    {
        for (var j = before.Count - 1; j >= 0; j--)
        {
            var (candle, chain) = before[j];
            if (within != null && at - candle > within)
                return null;
            if (candle < first)
                return chain;
        }

        return null;
    }

    /// <summary>
    /// Whether a step is met at the close of candle index.
    /// </summary>
    public static bool Enters(IReadOnlyList<Price> prices, int index, StrategyEntry entry)
    {
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
    /// Every candle that meets the strategy's entry condition, found another way from Signals, to check the trades against:
    /// each step's candles as <see cref="Occurrences(List{Price}, StrategyEntry)"/> finds them, then each candle of the last
    /// step that a chain of the steps before it can be traced back from, all after the candle the chain was last met at.
    /// </summary>
    public static List<int> Occurrences(List<Price> prices, Strategy strategy)
    {
        var steps = strategy.Steps;
        var each = steps.Select(x => Occurrences(prices, x)).ToList();
        var found = new List<int>();
        var after = -1;
        foreach (var at in each[^1])
        {
            if (!Traced(steps.Count - 1, at))
                continue;
            found.Add(at);
            after = at;
        }

        return found;

        // Whether a candle that meets step k has, before it, a candle meeting each step before k in turn.
        bool Traced(int k, int at)
        {
            if (k == 0)
                return true;
            var step = steps[k];
            return each[k - 1].Where(x => x > after && x < at - step.Reads && (step.Within == null || at - x <= step.Within)).Any(x => Traced(k - 1, x));
        }
    }

    /// <summary>
    /// Every candle that meets a step, found another way from Enters: for SuccessiveCandles, the Length-th candle of each
    /// run of at least Length candles of the colour, from CandleRuns.
    /// </summary>
    public static List<int> Occurrences(List<Price> prices, StrategyEntry entry)
    {
        var index = prices.Select((p, i) => (p.DateTime, i)).ToDictionary(x => x.DateTime, x => x.i);
        return CandleRuns.Runs(prices, entry.Length)
            .Where(r => r.Green == (entry.Colour == EnumCandleColour.Green))
            .Select(r => index[r.Start.Time] + entry.Length - 1)
            .ToList();
    }

    /// <summary>
    /// Whether a trade entered at the close of candle index can have both its targets set there (see
    /// <see cref="StrategyTarget.Price"/>); a candle where one cannot is passed over.
    /// </summary>
    public static bool Measurable(IReadOnlyList<Price> prices, int index, Strategy strategy) =>
        strategy.TakeProfit.Price(prices, index, strategy.Direction, profit: true) is double takeProfit
        && strategy.StopLoss.Price(prices, index, strategy.Direction, profit: false, takeProfit) != null;

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
            closed.Count(x => x.BothTouched));
    }

    /// <summary>
    /// The trades as CSV, a row each, for spreadsheets and charts.
    /// </summary>
    public static string ToCsv(IEnumerable<StrategyTrade> trades)
    {
        var csv = new StringBuilder("Number,Direction,EntryTime,EntryPrice,TakeProfit,StopLoss,ExitTime,ExitPrice,Outcome,Profit,ProfitPercent,Candles,BothTouched\n");
        foreach (var t in trades)
        {
            csv.AppendLine(string.Join(",", t.Number, t.Direction, Time(t.EntryTime), Number(t.EntryPrice), Number(t.TakeProfit), Number(t.StopLoss),
                Time(t.ExitTime), Number(t.ExitPrice), t.Outcome, Number(t.Profit), Number(t.ProfitPercent), t.Candles, t.BothTouched));
        }

        return csv.ToString();
    }

    private static string Time(DateTime time) => time.ToString("yyyy-MM-ddTHH:mm", CultureInfo.InvariantCulture);

    private static string Number(double value) => value.ToString("0.########", CultureInfo.InvariantCulture);

    private static StrategyTrade? Trade(IReadOnlyList<Price> prices, int index, Strategy strategy, int number, IReadOnlyList<StrategyStep> met)
    {
        var entry = prices[index].Close;
        if (strategy.TakeProfit.Price(prices, index, strategy.Direction, profit: true) is not double takeProfit
            || strategy.StopLoss.Price(prices, index, strategy.Direction, profit: false, takeProfit) is not double stopLoss)
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

        StrategyTrade Made(int exitIndex, double exitPrice, EnumTradeOutcome outcome, bool both)
        {
            var profit = sign * (exitPrice - entry);
            return new StrategyTrade(number, strategy.Direction, index, prices[index].DateTime, entry, takeProfit, stopLoss,
                exitIndex, prices[exitIndex].DateTime, exitPrice, outcome, profit, 100 * profit / entry, exitIndex - index, both, met);
        }
    }

    private static EnumCandleColour? Colour(Price price) =>
        price.Close > price.Open ? EnumCandleColour.Green : price.Close < price.Open ? EnumCandleColour.Red : null;
}
