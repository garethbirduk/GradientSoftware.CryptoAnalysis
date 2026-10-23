namespace Gradient.CryptoAnalysis.Strategies;

/// <summary>
/// A trade of a round: the backtest's trade, the money it made on the round's stake, and whether it was refunded for
/// closing after the round's window, in which case it made nothing either way.
/// </summary>
public sealed record GameTrade(StrategyTrade Trade, double Money, bool Refunded);

/// <summary>
/// A strategy in a round: its trades entered in the window, with the money they made in all and how many were refunded.
/// </summary>
public sealed record GameContender(Strategy Strategy, List<GameTrade> Trades, double Total, int Refunded);

/// <summary>
/// A round of the game: a window of a dataset's candles, first up to but not including end, the stake a trade, and what
/// each strategy made in it. Winner is the contender that made the most, the first of them when they tie.
/// </summary>
public sealed record GameRound(string Dataset, int First, int End, DateTime From, DateTime To, int Candles, double Stake,
    List<GameContender> Contenders, int Winner);

/// <summary>
/// Which strategy would have made you the most money? A round deals a window of candles and runs each strategy on it with
/// a fixed stake a trade, so that a strategy that wins one trade and loses the next is seen to. The window is picked at
/// random from the dataset, around a signal of the strategy that enters least often: one every strategy trades in, and
/// that one of them wins, when such a window turns up within the tries; otherwise the one most of them trade in.
/// </summary>
public static class StrategyGame
{
    /// <summary>
    /// How many windows are tried for one every strategy trades in.
    /// </summary>
    public const int Tries = 200;

    /// <summary>
    /// Deals a round of the given number of candles from the prices. Each strategy's signals are read from the whole dataset
    /// (see <see cref="StrategyBacktest.Signals"/>), or from signalsOf when given, so a server can keep them between rounds.
    /// </summary>
    public static GameRound Round(IReadOnlyList<Price> prices, string dataset, IReadOnlyList<Strategy> strategies, int candles, double stake, Random random,
        Func<Strategy, List<(int Index, IReadOnlyList<int> Met)>>? signalsOf = null)
    {
        if (strategies.Count == 0)
            throw new ArgumentException("A round needs a strategy.", nameof(strategies));
        if (candles < 1 || candles > prices.Count)
            throw new ArgumentException($"A round is from 1 to {prices.Count} candles.", nameof(candles));

        var signals = strategies.Select(s => signalsOf?.Invoke(s) ?? StrategyBacktest.Signals(prices, s)).ToList();
        var latest = prices.Count - candles;
        // A window is placed around a signal of the strategy with the fewest, so a strategy that seldom enters is dealt a
        // window it enters in, rather than waited for; with no signals at all, anywhere.
        var rarest = signals.MinBy(x => x.Count)!;
        (int First, List<List<GameTrade>> Trades, int Trading)? best = null;
        for (var tries = 0; tries < Tries; tries++)
        {
            var first = rarest.Count == 0 ? random.Next(0, latest + 1)
                : Math.Clamp(rarest[random.Next(rarest.Count)].Index - random.Next(candles), 0, latest);
            var trades = strategies.Select((s, k) => TradesIn(prices, s, signals[k], first, first + candles, stake)).ToList();
            var trading = trades.Count(t => t.Count > 0);
            var decided = trades.Select(t => t.Sum(x => x.Money)).Distinct().Count() > 1;
            if (trading == strategies.Count && decided)
            {
                best = (first, trades, trading);
                break;
            }

            if (best == null || trading > best.Value.Trading)
                best = (first, trades, trading);
        }

        var (start, dealt, _) = best!.Value;
        var end = start + candles;
        var contenders = strategies.Select((s, k) => new GameContender(s, dealt[k], dealt[k].Sum(x => x.Money), dealt[k].Count(x => x.Refunded))).ToList();
        var winner = contenders.Select((c, k) => (c.Total, k)).OrderByDescending(x => x.Total).ThenBy(x => x.k).First().k;
        return new GameRound(dataset, start, end, prices[start].DateTime, prices[end - 1].DateTime, candles, stake, contenders, winner);
    }

    /// <summary>
    /// The strategy's trades entered at the candles from first up to but not including end, each with its money on the
    /// stake: a share of the stake as the trade's profit is of its entry price. A trade still open at the window's end is
    /// refunded.
    /// </summary>
    public static List<GameTrade> TradesIn(IReadOnlyList<Price> prices, Strategy strategy, IReadOnlyList<(int Index, IReadOnlyList<int> Met)> signals, int first, int end, double stake)
    {
        var trades = StrategyBacktest.Trades(prices, strategy, signals.Where(x => x.Index >= first && x.Index < end));
        return trades.Select(t =>
        {
            var refunded = t.Outcome == EnumTradeOutcome.Open || t.ExitIndex >= end;
            return new GameTrade(t, refunded ? 0 : stake * t.ProfitPercent / 100, refunded);
        }).ToList();
    }
}
