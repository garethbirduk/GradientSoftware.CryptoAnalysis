using Gradient.CryptoAnalysis.Site;

namespace Gradient.CryptoAnalysis.Test.Site;

/// <summary>
/// The page's List: where each kind of thing occurs in a dataset, as known at its last candle.
/// </summary>
[TestClass]
public class OccurrencesTests
{
    private static readonly Lazy<StructureStore> Store = new(() => StructureStore.Read(Btc.From2023()));

    [TestMethod]
    public void Of_EveryKind_ListsItsOccurrencesInTheOrderTheyBegin()
    {
        var store = Store.Value;
        var last = store.Candles - 1;

        foreach (var kind in Occurrences.Kinds)
        {
            var rows = Occurrences.Of(store, kind, 8)!.Select(x => x!.AsObject()).ToList();

            Assert.IsTrue(rows.Count > 5, $"{kind}: {rows.Count}");
            CollectionAssert.AreEqual(rows.Select(x => x["index"]!.GetValue<int>()).Order().ToList(), rows.Select(x => x["index"]!.GetValue<int>()).ToList(), kind);
            foreach (var row in rows)
            {
                var (index, level, candles) = (row["index"]!.GetValue<int>(), row["level"]!.GetValue<int>(), row["candles"]!.GetValue<int>());
                Assert.IsTrue(index >= 0 && index + candles <= last && level is >= 1 and <= 8, $"{kind}: {row.ToJsonString()}");
                Assert.AreEqual(store.Prices[index].DateTime.ToString("yyyy-MM-ddTHH:mm"), row["start"]!.GetValue<string>());
                Assert.AreEqual(store.Prices[index + candles].DateTime.ToString("yyyy-MM-ddTHH:mm"), row["until"]!.GetValue<string>());
                if (kind != "DeepMsb")
                    StringAssert.Matches(row["thing"]!.GetValue<string>(), new System.Text.RegularExpressions.Regex("^(Swing|Trend|SwingRange):[0-9]+$"));
            }
        }
    }

    [TestMethod]
    public void Of_ARangeBySwings_HasItsEndsItsHeightAndTheSwingThatIdentifiedIt()
    {
        var store = Store.Value;
        var found = SwingRanges.Over(store, 4, 0, store.Candles - 1, store.Candles - 1).Where(x => x.Level <= 8).ToList();

        var rows = Occurrences.Of(store, "SwingRange", 8, 4)!.Select(x => x!.AsObject()).ToList();

        Assert.AreEqual(found.Count, rows.Count);
        var (range, row) = (found[0], rows[0]);
        Assert.AreEqual((range.Identified, range.Level, range.Swings, range.Weak, $"SwingRange:{range.Level}"),
            (row["index"]!.GetValue<int>(), row["level"]!.GetValue<int>(), row["swings"]!.GetValue<int>(), row["weak"]!.GetValue<int>(), row["thing"]!.GetValue<string>()));
        Assert.IsTrue(row["high"]!.GetValue<double>() > row["low"]!.GetValue<double>() && row["height"]!.GetValue<double>() > 0);
        Assert.IsTrue(Occurrences.Of(store, "SwingRange", 8, 6)!.Count < rows.Count, "A greater count of Swings finds fewer.");
    }

    [TestMethod]
    public void Of_ARangeBySwings_OpensAnAnalysisOfThatRange()
    {
        var prices = Btc.From2023().Take(3000).ToList();
        var store = StructureStore.Read(prices);
        // One that ended, so the analysis tells its end.
        var row = Occurrences.Of(store, "SwingRange", 8)!.Select(x => x!.AsObject()).First(x => x["end"] != null && x["candles"]!.GetValue<int>() > 20);
        var thing = Explain.Thing.Parse(row["thing"]!.GetValue<string>())!;
        var (low, high) = (Math.Floor(row["low"]!.GetValue<double>()), Math.Floor(row["high"]!.GetValue<double>()));

        var tour = Explain.Tour([thing], "btc", prices, row["at"]!.GetValue<string>(), null, out var missing);
        var written = Explain.SwingRange(prices, row["index"]!.GetValue<int>(), row["level"]!.GetValue<int>(), 0, out var problem);

        Assert.AreEqual(0, missing.Count, string.Join(", ", missing));
        var chapter = tour!["sections"]!.AsArray().Single()!.AsObject();
        Assert.AreEqual(("SwingRange", row["level"]!.GetValue<int>()), (chapter["explain"]!.GetValue<string>(), chapter["level"]!.GetValue<int>()));
        CollectionAssert.Contains(chapter["layers"]!.AsArray().Select(x => x!.GetValue<string>()).ToList(), "SwingRange", "The chapter draws the Range.");
        Assert.IsTrue(chapter["from"]!.GetValue<int>() < row["index"]!.GetValue<int>() && chapter["until"]!.GetValue<int>() == row["index"]!.GetValue<int>() + row["candles"]!.GetValue<int>());

        Assert.AreEqual("", problem);
        var texts = string.Join(" ", written!["cues"]!.AsArray().Select(x => x!["texts"]!.ToJsonString()));
        StringAssert.Contains(texts, $"from {low} to {high}");
        StringAssert.Contains(texts, "Ended at");
        StringAssert.Contains(texts, "A Range by Swings is found inside the last leg of an order");
        Assert.IsNull(Explain.SwingRange(prices, 5, 1, 0, out problem));
        StringAssert.Contains(problem, "no Range by Swings");
    }

    [TestMethod]
    public void Of_AKindThatIsNotListed_IsNull()
    {
        Assert.IsNull(Occurrences.Of(Store.Value, "HigherHigh", 8));
    }
}
