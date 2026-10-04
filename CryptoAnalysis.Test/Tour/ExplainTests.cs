using System.Text.Json.Nodes;
using Gradient.CryptoAnalysis.Site;

namespace Gradient.CryptoAnalysis.Test.Tour;

/// <summary>
/// Checks the sections the tour writes from the prices (see <see cref="Explain"/>): what a Candle section says about the
/// tour's own example candle, how it reads a red one and a four-hour one, and how what a section says for itself is kept.
/// </summary>
[TestClass]
public class ExplainTests
{
    private static readonly string RepoRoot = FindRepoRoot(AppContext.BaseDirectory);

    // The tour's dataset from its start time, 2023-01-01, so the candles are numbered as the tour numbers them.
    private static readonly Lazy<List<Price>> TourPrices = new(() =>
    {
        var prices = Tours.Load(Tours.Datasets[0], RepoRoot);
        return prices.Skip(Tours.AnchorOf("2023-01-01T00:00", prices)).ToList();
    });

    private static List<string> Texts(JsonObject section) => (section["cues"]?.AsArray() ?? []).Select(x => x!["text"]!.GetValue<string>()).ToList();

    [TestMethod]
    public void Candle_TheToursExample_SaysItsPricesAndWhyItIsGreen()
    {
        var section = Explain.Candle(TourPrices.Value, 48, reached: 50);

        CollectionAssert.AreEqual(new[] { 44, 51 }, section["view"]!.AsArray().Select(x => x!.GetValue<int>()).ToArray());
        Assert.IsNull(section["until"], "The replay has passed the candle, so the section does not run on.");
        CollectionAssert.AreEqual(new[]
        {
            "This candle is the hour from 00:00 to 01:00 on Tuesday 3 January 2023.",
            "It opened at 16666 and closed at 16690.",
            "open: 16666",
            "close: 16690",
            "Its high was 16700 and its low was 16639.",
            "high: 16700",
            "low: 16639",
            "Because the candle closed higher than the open, it is green.",
            "The candle before it closed lower than the open, so it is red.",
            "red",
        }, Texts(section));

        var pins = section["cues"]!.AsArray().Where(x => x!["on"] != null).Select(x => $"#{x!["at"]} {x["on"]} {x["place"]}").ToList();
        CollectionAssert.AreEqual(new[] { "#48 open left", "#48 close right", "#48 high above", "#48 low below", "#47 low below" }, pins);
        Assert.IsTrue(section["cues"]!.AsArray().Where(x => x!["on"] != null).All(x => x!["voice"]?.GetValue<bool>() == false), "Pins are shown, not read.");
    }

    [TestMethod]
    public void Candle_RedCandle_SetsTheNextGreenOneAgainstIt()
    {
        var section = Explain.Candle(TourPrices.Value, 47, reached: 50);
        var texts = Texts(section);

        Assert.AreEqual("Because the candle closed lower than the open, it is red.", texts[7]);
        Assert.AreEqual("The candle after it closed higher than the open, so it is green.", texts[8]);
        var contrast = section["cues"]!.AsArray()[^1]!;
        Assert.AreEqual(48, contrast["at"]!.GetValue<int>());
        Assert.AreEqual("high", contrast["on"]!.GetValue<string>());
        Assert.AreEqual("above", contrast["place"]!.GetValue<string>());
    }

    [TestMethod]
    public void Candle_NotYetReached_RunsOnToItAndContrastsOnlyWithDrawnCandles()
    {
        var section = Explain.Candle(TourPrices.Value, 48, reached: 40);

        Assert.AreEqual(48, section["until"]!.GetValue<int>());
        var contrast = section["cues"]!.AsArray()[^1]!;
        Assert.IsTrue(contrast["at"]!.GetValue<int>() <= 48, "A candle after the one explained is not drawn yet.");
    }

    [TestMethod]
    public void Candle_FourHourCandles_NamesTheHoursSpanned()
    {
        var start = new DateTime(2021, 11, 10, 16, 0, 0, DateTimeKind.Utc);
        var prices = Enumerable.Range(0, 8).Select(i => new Price { DateTime = start.AddHours(4 * i), Open = 100 + i, High = 110 + i, Low = 90 + i, Close = 105 + i }).ToList();

        var texts = Texts(Explain.Candle(prices, 2, reached: 7));

        Assert.AreEqual("This candle is the four hours from 00:00 to 04:00 on Thursday 11 November 2021.", texts[0]);
        Assert.AreEqual("Because the candle closed higher than the open, it is green.", texts[7]);
        Assert.AreEqual(8, texts.Count, "Every candle is green, so there is none to set against it.");
    }

    [TestMethod]
    public void Expand_KeepsWhatTheSectionSaysForItself()
    {
        var node = JsonNode.Parse("""{ "chapter": "Candles", "explain": "Candle", "at": 48, "view": [40, 60], "speed": 2, "cues": [{ "text": "That is one candle." }] }""")!.AsObject();
        var errors = new List<string>();

        var section = Explain.Expand(node, TourPrices.Value, 50, "section 9", errors);

        Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
        Assert.IsNull(section["explain"]);
        Assert.IsNull(section["at"]);
        Assert.AreEqual("Candles", section["chapter"]!.GetValue<string>());
        Assert.AreEqual(2, section["speed"]!.GetValue<int>());
        CollectionAssert.AreEqual(new[] { 40, 60 }, section["view"]!.AsArray().Select(x => x!.GetValue<int>()).ToArray());
        Assert.AreEqual("That is one candle.", Texts(section)[^1]);
        Assert.AreEqual(11, Texts(section).Count);
    }

    [TestMethod]
    public void Swing_TheToursTenthUpswing_IsWeakWithItsMsbOnTheWay()
    {
        // The Upswing of the tour's "Wicks and breaks" chapter: from the HH at #45 to the BoS at #75, with the MSB at #46 inside it.
        var section = Explain.Swing(TourPrices.Value, 60, level: 1, reached: 50, out var problem);

        Assert.IsNotNull(section, problem);
        Assert.AreEqual(75, section["until"]!.GetValue<int>());
        CollectionAssert.AreEqual(new[] { 39, 81 }, section["view"]!.AsArray().Select(x => x!.GetValue<int>()).ToArray());
        var pins = section["cues"]!.AsArray().Where(x => x!["on"] != null).Select(x => $"#{x!["at"]} {x["text"]}").ToList();
        Assert.IsTrue(Texts(section).Any(x => x.StartsWith("This is a 1st order Upswing. It begins")));
        Assert.IsTrue(Texts(section).Contains("The MSB inside it makes it a Weak Upswing."));
        Assert.IsTrue(pins.Contains("#46 MSB"));
        Assert.IsTrue(pins.Contains("#75 BoS"));
    }

    [TestMethod]
    public void Swing_NoneAtTheCandle_SaysSo()
    {
        var section = Explain.Swing(TourPrices.Value, 0, level: 1, reached: 0, out var problem);

        Assert.IsNull(section);
        Assert.AreEqual("no 1st order Swing has #0 in it", problem);
    }

    [TestMethod]
    public void Trend_TheToursUptrend_HasTwelveUpswingsFourWeak()
    {
        // The tour's Uptrend as seen at #90: twelve Upswings, the fourth, seventh, tenth and twelfth Weak, Strength 66%.
        var section = Explain.Trend(TourPrices.Value, 90, level: 1, reached: 90, out var problem);

        Assert.IsNotNull(section, problem);
        var texts = Texts(section);
        Assert.IsNull(section["until"], "The replay is already at the candle.");
        Assert.IsTrue(texts.Contains("Here it is an Uptrend of twelve Upswings."), texts[4]);
        Assert.IsTrue(texts.Contains("Four of the twelve are Weak: the fourth, seventh, tenth and twelfth. Each has an MSB inside it, a close below the Swing Low of the Upswing before."), texts[5]);
        Assert.IsTrue(texts.Contains("Its Strength is eight of twelve: 66%."), texts[6]);
        Assert.IsTrue(texts.Contains("No Downswing has been Confirmed since, so it is still running."), texts[7]);
    }

    [TestMethod]
    public void Trend_SeenBeforeTheNewHigh_EndedAtTheFirstDownswing()
    {
        // At #64 the Uptrend has ended at the BoS of the first Downswing (#62), as the tour's "Trends in real time" chapter shows.
        var section = Explain.Trend(TourPrices.Value, 40, level: 1, reached: 64, out var problem);

        Assert.IsNotNull(section, problem);
        var texts = Texts(section);
        Assert.IsTrue(texts.Any(x => x.StartsWith("By its end it is an Uptrend of")), texts[4]);
        Assert.IsTrue(texts.Any(x => x.StartsWith("It ended at") && x.EndsWith("at the BoS of the first Downswing since it began.")), texts[^1]);
        var end = section["cues"]!.AsArray().Last(x => x!["on"] != null)!;
        Assert.AreEqual("Uptrend ends", end["text"]!.GetValue<string>());
        Assert.AreEqual(62, end["at"]!.GetValue<int>());
    }

    [TestMethod]
    public void Tour_AroundASwing_JumpsToItAndRunsThroughIt()
    {
        var prices = Tours.Load(Tours.Datasets[0], RepoRoot);

        var def = Explain.Tour("Swing", "btc-1h", prices, "2023-01-03T12:00")!;
        var tour = Tours.Compile(def, prices);

        Assert.AreEqual(0, tour.Errors.Count, string.Join("\n", tour.Errors));
        Assert.AreEqual(1, tour.Sections.Count);
        Assert.AreEqual(39, tour.Sections[0].From, "Six candles before the Upswing begins at #45.");
        Assert.AreEqual(75, tour.Sections[0].Until, "The BoS.");
        StringAssert.StartsWith(def["title"]!.GetValue<string>(), "1st order Swing at 12:00 on 3 January 2023");
        Assert.IsNull(Explain.Tour("Trend", "btc-1h", prices, "2023-01-01T00:00", level: 8), "No level 8 Trend has the first candle.");

        // A Trend runs to the candle, and is the one the Replay page showed at its cursor: at #64 the Uptrend has ended.
        var trend = Tours.Compile(Explain.Tour("Trend", "btc-1h", prices, "2023-01-02T12:00", seen: "2023-01-03T16:00")!, prices);
        Assert.AreEqual(0, trend.Errors.Count, string.Join("\n", trend.Errors));
        Assert.AreEqual(36, trend.Sections[0].Until, "The candle clicked.");
        Assert.IsTrue(trend.Sections[0].Cues.Any(c => c.Text == "No Downswing has been Confirmed since, so it is still running."), "Seen from #36 the Uptrend still runs.");
        Assert.IsNull(Explain.Tour("Swing", "btc-1h", prices, "2023-01-03T12:00", seen: "2023-01-03T13:00"), "At #61 the Upswing from #45 has no BoS yet, so the page did not show it.");

        // A 2nd order Swing the page showed at #100 is not on the chart at its own BoS, so the tour runs on to the cursor.
        var finer = Tours.Compile(Explain.Tour("Swing", "btc-1h", prices, "2023-01-03T12:00", level: 2, seen: "2023-01-05T04:00")!, prices);
        Assert.AreEqual(0, finer.Errors.Count, string.Join("\n", finer.Errors));
        Assert.AreEqual(100, finer.Sections[0].Until);
        Assert.IsTrue(finer.Sections[0].Cues.Any(c => c.Text.StartsWith("This is a 2nd order ")), finer.Sections[0].Cues[0].Text);

        // At #290 the Uptrend had ended at a Downswing's BoS; by the cursor those Downswings were Ghosts and it ran on, so the tour runs to the cursor.
        var ghosted = Tours.Compile(Explain.Tour("Trend", "btc-1h", prices, "2023-01-13T02:00", seen: "2023-01-14T00:00")!, prices);
        Assert.AreEqual(0, ghosted.Errors.Count, string.Join("\n", ghosted.Errors));
        Assert.AreEqual(312, ghosted.Sections[0].Until);
    }

    [TestMethod]
    public void Tour_RunsToTheCandleThenExplainsIt()
    {
        var prices = Tours.Load(Tours.Datasets[0], RepoRoot);

        var def = Explain.Tour("Candle", "btc-1h", prices, "2023-01-03T00:00")!;
        var tour = Tours.Compile(def, prices);

        Assert.AreEqual("Candle at 00:00 on 3 January 2023", def["title"]!.GetValue<string>());
        Assert.AreEqual("2023-01-02T16:00", def["start"]!["time"]!.GetValue<string>());
        Assert.AreEqual(0, tour.Errors.Count, string.Join("\n", tour.Errors));
        Assert.AreEqual(2, tour.Sections.Count);
        Assert.AreEqual(8, tour.Sections[0].Until, "The first section runs to the candle, saying nothing.");
        Assert.AreEqual(0, tour.Sections[0].Cues.Count);
        Assert.AreEqual(8, tour.Sections[1].From, "The section that explains the candle begins with it drawn.");
        Assert.AreEqual("This candle is the hour from 00:00 to 01:00 on Tuesday 3 January 2023.", tour.Sections[1].Cues[0].Text);
        Assert.IsNull(Explain.Tour("Weather", "btc-1h", prices, "2023-01-03T00:00"));
    }

    [TestMethod]
    public void Expand_ListsWhatItCannotExplain()
    {
        var errors = new List<string>();

        Explain.Expand(JsonNode.Parse("""{ "explain": "Weather", "at": 48 }""")!.AsObject(), TourPrices.Value, 50, "section 9", errors);
        Explain.Expand(JsonNode.Parse("""{ "explain": "Candle" }""")!.AsObject(), TourPrices.Value, 50, "section 10", errors);
        Explain.Expand(JsonNode.Parse("""{ "explain": "Candle", "at": 999999 }""")!.AsObject(), TourPrices.Value, 50, "section 11", errors);

        Assert.AreEqual(3, errors.Count, string.Join("\n", errors));
        StringAssert.StartsWith(errors[0], "section 9: explain names \"Weather\"");
        Assert.AreEqual("section 10: explain needs \"at\", the candle to explain", errors[1]);
        StringAssert.StartsWith(errors[2], "section 11: at #999999 is not a candle of the tour");
    }

    private static string FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CryptoAnalysis.sln")))
                return dir.FullName;
        }

        throw new InvalidOperationException("The repository root (CryptoAnalysis.sln) was not found.");
    }
}
