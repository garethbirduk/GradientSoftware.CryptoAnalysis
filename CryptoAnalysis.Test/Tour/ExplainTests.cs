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

    // A section's texts as the page shows them at a level of detail: each cue's text for the highest level at or below it,
    // leaving out the cues with none that low.
    private static List<string> Texts(JsonObject section, EnumDetail detail = EnumDetail.Summary) => (section["cues"]?.AsArray() ?? [])
        .Select(x => Enum.GetValues<EnumDetail>().Where(level => level <= detail).Select(level => x!["texts"]?[Details.Name(level)]?.GetValue<string>()).LastOrDefault(t => t != null))
        .Where(x => x != null).Select(x => x!).ToList();

    [TestMethod]
    public void Candle_TheToursExample_SaysItsPricesAndWhyItIsGreen()
    {
        var section = Explain.Candle(TourPrices.Value, 48, reached: 50);

        CollectionAssert.AreEqual(new[] { 44, 51 }, section["view"]!.AsArray().Select(x => x!.GetValue<int>()).ToArray());
        Assert.IsNull(section["until"], "The replay has passed the candle, so the section does not run on.");
        CollectionAssert.AreEqual(new[]
        {
            "The hour from 00:00 to 01:00 on Tuesday 3 January 2023.",
            "Open 16666, close 16690.",
            "open: 16666",
            "close: 16690",
            "High 16700, low 16639.",
            "high: 16700",
            "low: 16639",
            "Green: it closed above its open.",
        }, Texts(section));

        // Every pin, at Education, where the candle set against it is pinned too.
        var pins = section["cues"]!.AsArray().Where(x => x!["on"] != null).Select(x => $"#{x!["at"]} {x["on"]} {x["place"]}").ToList();
        CollectionAssert.AreEqual(new[] { "#48 open left", "#48 close right", "#48 high above", "#48 low below", "#47 low below" }, pins);
        Assert.IsTrue(section["cues"]!.AsArray().Where(x => x!["on"] != null).All(x => x!["voice"]?.GetValue<bool>() == false), "Pins are shown, not read.");
        CollectionAssert.AreEqual(new[] { "candles" }, section["add"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray(), "Not in a run of five, so no Successive Candles.");
    }

    [TestMethod]
    public void Candle_AtEducation_TeachesWhatACandleIsBetweenItsPrices()
    {
        var section = Explain.Candle(TourPrices.Value, 48, reached: 50);

        CollectionAssert.AreEqual(new[]
        {
            "A candle shows a whole hour of trading in one shape, drawn from four prices.",
            "The hour from 00:00 to 01:00 on Tuesday 3 January 2023.",
            "The open is the price at the start of the hour and the close is the price at its end. The body of the candle spans the two.",
            "Open 16666, close 16690.",
            "open: 16666",
            "close: 16690",
            "The high and the low are the furthest the price went during the hour. The thin lines above and below the body are wicks, and they reach to them.",
            "High 16700, low 16639.",
            "high: 16700",
            "low: 16639",
            "A candle that closes above its open is green. A candle that closes below its open is red. One that closes where it opened has no body, and is neither.",
            "Green: it closed above its open.",
            "The candle before it closed lower than the open, so it is red.",
            "red",
        }, Texts(section, EnumDetail.Education));
    }

    [TestMethod]
    public void Run_TellsOfTheSuccessiveCandlesByThePagesSetting()
    {
        // The tour's six green candles, #85 to #90: #87 is the third of them, told of on the run's last candle.
        var section = Explain.Run(TourPrices.Value, 87, reached: 90, null, null, out var problem)!;

        Assert.IsNotNull(section, problem);
        CollectionAssert.AreEqual(new[] { "Third of six Successive Green Candles, 13:00 to 18:00." }, Texts(section));
        Assert.AreEqual("Candles of one colour that follow one another are Successive Candles, counted from the first of the colour to the last.", Texts(section, EnumDetail.Education)[0]);
        CollectionAssert.AreEqual(new[] { "candles", "SuccessiveGreenCandles" }, section["add"]!.AsArray().Select(x => x!.GetValue<string>()).ToArray());
        CollectionAssert.AreEqual(new[] { 83, 92 }, section["view"]!.AsArray().Select(x => x!.GetValue<int>()).ToArray(), "The view takes in the whole run.");
        Assert.IsFalse(Texts(Explain.Candle(TourPrices.Value, 87, reached: 90)).Any(x => x.Contains("Successive")), "The Candle itself does not tell of its run.");

        // Drawn only to #87, the run is three so far; with a setting of seven there is none; seen from #90 it is whole.
        Assert.AreEqual("Third of three Successive Green Candles, 13:00 to 15:00.", Texts(Explain.Run(TourPrices.Value, 87, reached: 80, new ExplainOptions(3, 3), null, out _)!)[0]);
        Assert.IsNull(Explain.Run(TourPrices.Value, 87, reached: 90, new ExplainOptions(7, 7), null, out _));
        var seen = Explain.Run(TourPrices.Value, 87, reached: 87, null, known: 90, out _)!;
        Assert.AreEqual(90, seen["until"]!.GetValue<int>());
    }

    [TestMethod]
    public void At_ListsWhatIsAtTheCandle_AndATourHasAChapterForEachThingTicked()
    {
        var prices = Tours.Load(Tours.Datasets[0], RepoRoot);

        // The third of the tour's six green candles, as the Replay page had it at the run's last candle.
        var things = Explain.At(prices, "2023-01-04T15:00", "2023-01-04T18:00");

        Assert.AreEqual("Candle", things[0].Id);
        Assert.AreEqual("SuccessiveCandles", things[1].Id);
        Assert.AreEqual("Six Successive Green Candles", things[1].Name);
        Assert.IsTrue(things.Any(x => x.Id == "Swing:1"), string.Join(", ", things.Select(x => x.Id)));
        var levels = things.Where(x => x.Level != null).Select(x => x.Level!.Value).ToList();
        CollectionAssert.AreEqual(levels.OrderByDescending(x => x).ToList(), levels, "From the finest level outwards.");

        var def = Explain.Tour([new Explain.Thing("Swing", 1, "Swing"), new Explain.Thing("Candle", null, "Candle"), new Explain.Thing("SuccessiveCandles", null, "Run"), new Explain.Thing("Trend", 8, "Trend")],
            "btc-1h", prices, "2023-01-04T15:00", "2023-01-04T18:00", out var missing)!;
        var tour = Tours.Compile(def, prices);

        Assert.AreEqual(0, tour.Errors.Count, string.Join("\n", tour.Errors));
        CollectionAssert.AreEqual(new[] { "Candle", "Successive Candles", "1st order Upswing" }, tour.Sections.Select(x => x.Chapter).ToArray(), "From the candle outwards.");
        CollectionAssert.AreEqual(new[] { "Trend" }, missing, "No 8th order Trend there.");
        Assert.AreEqual(87, tour.Sections[0].Until, "The Candle chapter runs to the candle.");
        Assert.AreEqual(90, tour.Sections[1].Until, "The run's chapter runs to its last candle.");
        Assert.IsTrue(tour.Sections[2].Cues.Any(c => c.Text.StartsWith("1st order Upswing, from ")), tour.Sections[2].Cues[1].Text);
    }

    [TestMethod]
    public void Point_TellsOfTheHighAgainstTheOneBefore()
    {
        var prices = Tours.Load(Tours.Datasets[0], RepoRoot);
        var point = Explain.At(prices, "2023-01-04T18:00", "2023-01-04T18:00").First(x => x.Kind == "Point");

        var tour = Tours.Compile(Explain.Tour([point], "btc-1h", prices, "2023-01-04T18:00", "2023-01-04T18:00", out _)!, prices);

        Assert.AreEqual(0, tour.Errors.Count, string.Join("\n", tour.Errors));
        var texts = tour.Sections[0].Cues.Where(c => c.On == null).Select(c => c.Text).ToList();
        Assert.AreEqual("A Higher High (HH) is a high of the Sawtooth at one order that is higher than the high before it at that order.", texts[0]);
        Assert.AreEqual("Highs and lows are read from the closes.", texts[1]);
        Assert.IsTrue(texts.Any(x => x.EndsWith("HH at 16948, above the high of 16872 at 06:00.")), string.Join("\n", texts));
    }

    [TestMethod]
    public void Candle_RedCandle_SetsTheNextGreenOneAgainstIt()
    {
        var section = Explain.Candle(TourPrices.Value, 47, reached: 50);
        var texts = Texts(section);

        Assert.AreEqual("Red: it closed below its open.", texts[7]);
        Assert.AreEqual("The candle after it closed higher than the open, so it is green.", Texts(section, EnumDetail.Education)[^2], "The candle set against it is for Education.");
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

        Assert.AreEqual("The four hours from 00:00 to 04:00 on Thursday 11 November 2021.", texts[0]);
        Assert.AreEqual("Green: it closed above its open.", texts[7]);
        Assert.AreEqual(12, Texts(Explain.Candle(prices, 2, reached: 7), EnumDetail.Education).Count, "Every candle is green, so there is none to set against it.");
    }

    [TestMethod]
    public void Expand_KeepsWhatTheSectionSaysForItself()
    {
        var node = JsonNode.Parse("""{ "chapter": "Candles", "explain": "Candle", "at": 48, "view": [40, 60], "speed": 2, "cues": [{ "texts": { "summary": "That is one candle." } }] }""")!.AsObject();
        var errors = new List<string>();

        var section = Explain.Expand(node, TourPrices.Value, 50, "section 9", errors);

        Assert.AreEqual(0, errors.Count, string.Join("\n", errors));
        Assert.IsNull(section["explain"]);
        Assert.IsNull(section["at"]);
        Assert.AreEqual("Candles", section["chapter"]!.GetValue<string>());
        Assert.AreEqual(2, section["speed"]!.GetValue<int>());
        CollectionAssert.AreEqual(new[] { 40, 60 }, section["view"]!.AsArray().Select(x => x!.GetValue<int>()).ToArray());
        Assert.AreEqual("That is one candle.", Texts(section)[^1]);
        Assert.AreEqual(9, Texts(section).Count);
    }

    [TestMethod]
    public void Swing_TheToursTenthUpswing_IsWeakWithItsMsbOnTheWay()
    {
        // The Upswing of the tour's "Wicks and breaks" chapter: from the HH at #45 to the BoS at #75, with the MSB at #46 inside it.
        var section = Explain.Swing(TourPrices.Value, 60, level: 1, reached: 50, out var problem);

        Assert.IsNotNull(section, problem);
        Assert.AreEqual(75, section["until"]!.GetValue<int>());
        CollectionAssert.AreEqual(new[] { 39, 81 }, section["view"]!.AsArray().Select(x => x!.GetValue<int>()).ToArray());
        var pins = section["cues"]!.AsArray().Where(x => x!["on"] != null).Select(x => $"#{x!["at"]} {x["texts"]!["summary"]}").ToList();
        CollectionAssert.AreEqual(new[]
        {
            "1st order Upswing, from an HH at 16751, at 21:00 on Monday 2 January 2023.",
            "MSB at 16693, below the Swing Low of the Upswing before.",
            "Swing Low: an LL at 16616.",
            "BoS at 16856.",
            "The Downleg is nineteen hours; the Upleg is eleven hours.",
            "Weak: it has an MSB.",
        }, Texts(section).Where(x => !pins.Any(p => p.EndsWith($" {x}"))).ToList());
        CollectionAssert.Contains(Texts(section, EnumDetail.Education), "An Upswing is characterised by a Downleg from the high to the Swing Low, followed by an Upleg from the Swing Low to the BoS.", "What its legs are is for Education.");
        CollectionAssert.Contains(Texts(section, EnumDetail.Education), Definitions.Text("Swing.weak", EnumSwingDirection.Up), "It has an MSB, so the Weak rules are told.");
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
        CollectionAssert.AreEqual(new[]
        {
            "1st order Uptrend, beginning at 01:00 on Sunday 1 January 2023.",
            "Uptrend begins",
            "Confirmed at the second Upswing's BoS, at 16:00 on Sunday 1 January 2023.",
            "second Upswing",
            "Twelve Upswings so far.",
            "Four Weak: the fourth, seventh, tenth and twelfth.",
            "Strength 66%: eight of twelve.",
            "Still running: no Downswing Confirmed since.",
        }, texts);
    }

    [TestMethod]
    public void Trend_SeenBeforeTheNewHigh_EndedAtTheFirstDownswing()
    {
        // At #64 the Uptrend has ended at the BoS of the first Downswing (#62), as the tour's "Trends in real time" chapter shows.
        var section = Explain.Trend(TourPrices.Value, 40, level: 1, reached: 64, out var problem);

        Assert.IsNotNull(section, problem);
        var texts = Texts(section);
        Assert.IsTrue(texts.Contains("Nine Upswings."), string.Join("\n", texts));
        Assert.IsTrue(texts.Contains("Ended at the first Downswing's BoS, at 14:00 on Tuesday 3 January 2023."), string.Join("\n", texts));
        var end = section["cues"]!.AsArray().Last(x => x!["on"] != null)!;
        Assert.AreEqual("Uptrend ends", end["texts"]!["summary"]!.GetValue<string>());
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
        Assert.AreEqual(45, tour.Sections[0].From, "The run begins on the Upswing's first candle.");
        Assert.AreEqual(75, tour.Sections[0].Until, "The BoS.");
        StringAssert.StartsWith(def["title"]!.GetValue<string>(), "1st order Swing at 12:00 on 3 January 2023");
        Assert.IsNull(Explain.Tour("Trend", "btc-1h", prices, "2023-01-01T00:00", level: 8), "No level 8 Trend has the first candle.");

        // A Trend runs to the candle, and is the one the Replay page showed at its cursor: at #64 the Uptrend has ended.
        var trend = Tours.Compile(Explain.Tour("Trend", "btc-1h", prices, "2023-01-02T12:00", seen: "2023-01-03T16:00")!, prices);
        Assert.AreEqual(0, trend.Errors.Count, string.Join("\n", trend.Errors));
        Assert.AreEqual(36, trend.Sections[0].Until, "The candle clicked.");
        Assert.IsTrue(trend.Sections[0].Cues.Any(c => c.Text == "Still running: no Downswing Confirmed since."), "Seen from #36 the Uptrend still runs.");
        Assert.IsNull(Explain.Tour("Swing", "btc-1h", prices, "2023-01-03T12:00", seen: "2023-01-03T13:00"), "At #61 the Upswing from #45 has no BoS yet, so the page did not show it.");

        // A 2nd order Swing the page showed at #100 is not on the chart at its own BoS, so the tour runs on to the cursor.
        var finer = Tours.Compile(Explain.Tour("Swing", "btc-1h", prices, "2023-01-03T12:00", level: 2, seen: "2023-01-05T04:00")!, prices);
        Assert.AreEqual(0, finer.Errors.Count, string.Join("\n", finer.Errors));
        Assert.AreEqual(100, finer.Sections[0].Until);
        Assert.IsTrue(finer.Sections[0].Cues.Any(c => c.Text.StartsWith("2nd order ")), finer.Sections[0].Cues[1].Text);

        // At #290 the Uptrend had ended at a Downswing's BoS; by the cursor those Downswings were Ghosts and it ran on, so the tour runs to the cursor.
        var ghosted = Tours.Compile(Explain.Tour("Trend", "btc-1h", prices, "2023-01-13T02:00", seen: "2023-01-14T00:00")!, prices);
        Assert.AreEqual(0, ghosted.Errors.Count, string.Join("\n", ghosted.Errors));
        Assert.AreEqual(312, ghosted.Sections[0].Until);
    }

    [TestMethod]
    public void AllTrends_ListsEveryLevelByStart_AndTheToursTrendIsAmongThem()
    {
        var prices = Tours.Load(Tours.Datasets[0], RepoRoot);

        var shown = Explain.TrendShown(prices, "2023-02-28T15:00", level: 3, seen: "2023-03-01T22:00")!;
        var trends = Explain.AllTrends(prices, 8).Select(x => x!.AsObject()).ToList();

        Assert.AreEqual("Down", shown["direction"]!.GetValue<string>());
        Assert.AreEqual(3, shown["level"]!.GetValue<int>());
        Assert.AreEqual(1387, shown["index"]!.GetValue<int>());
        var starts = trends.Select(x => x["index"]!.GetValue<int>()).ToList();
        CollectionAssert.AreEqual(starts.OrderBy(x => x).ToList(), starts);
        Assert.IsTrue(trends.Select(x => x["level"]!.GetValue<int>()).Distinct().Count() > 3, "Trends at several levels.");
        // The Downtrend the page showed at its cursor is a Ghost by the end of the dataset: the list has the Uptrend from #1343 there.
        Assert.AreEqual(2, shown["swings"]!.GetValue<int>());
        Assert.AreEqual(50, shown["strength"]!.GetValue<int>());
        Assert.IsFalse(trends.Any(x => x["level"]!.GetValue<int>() == 3 && x["index"]!.GetValue<int>() == 1387));
        Assert.IsTrue(trends.Any(x => x["level"]!.GetValue<int>() == 3 && x["index"]!.GetValue<int>() == 1343 && x["direction"]!.GetValue<string>() == "Up" && x["swings"]!.GetValue<int>() == 5 && x["strength"]!.GetValue<int>() == 80));
        Assert.IsNull(Explain.TrendShown(prices, "2023-01-01T00:00", level: 8));

        // A row a tour can be written of opens one that shows the same Trend and ends at its last Swing's BoS; the others
        // the chart had otherwise at the time.
        var withTour = trends.Where(x => x["tour"]!.GetValue<bool>()).ToList();
        Assert.IsTrue(withTour.Count > 0 && withTour.Count < trends.Count, $"{withTour.Count} of {trends.Count}");
        foreach (var row in withTour.Where((_, i) => i % 12 == 0))
        {
            var seen = Explain.TrendShown(prices, row["at"]!.GetValue<string>(), row["level"]!.GetValue<int>())!;
            Assert.AreEqual($"{row["direction"]} {row["swings"]} {row["strength"]} {row["start"]}", $"{seen["direction"]} {seen["swings"]} {seen["strength"]} {seen["start"]}");
            var tour = Tours.Compile(Explain.Tour("Trend", "btc-1h", prices, row["at"]!.GetValue<string>(), row["level"]!.GetValue<int>())!, prices);
            Assert.AreEqual(Tours.AnchorOf(row["at"]!.GetValue<string>(), prices), tour.Sections[0].Until);
        }

        Console.WriteLine($"{withTour.Count} of {trends.Count} Trends can have a tour");
    }

    [TestMethod]
    public void Tour_RunsToTheCandleThenExplainsIt()
    {
        var prices = Tours.Load(Tours.Datasets[0], RepoRoot);

        var def = Explain.Tour("Candle", "btc-1h", prices, "2023-01-03T00:00")!;
        var tour = Tours.Compile(def, prices);

        Assert.AreEqual("Candle at 00:00 on 3 January 2023", def["title"]!.GetValue<string>());
        Assert.AreEqual(0, tour.Errors.Count, string.Join("\n", tour.Errors));
        Assert.AreEqual(1, tour.Sections.Count);
        Assert.AreEqual(44, tour.Sections[0].From, "A few candles before it.");
        Assert.AreEqual(48, tour.Sections[0].Until, "It runs to the candle, then explains it.");
        CollectionAssert.AreEqual(new[] { 44, 51 }, tour.Sections[0].View.ToArray(), "The view is the candle and a few either side.");
        CollectionAssert.AreEqual(new[] { EnumDetail.Education }, tour.Sections[0].Cues[0].Texts.Keys.ToArray(), "What a candle is comes first, at Education only.");
        Assert.AreEqual("The hour from 00:00 to 01:00 on Tuesday 3 January 2023.", tour.Sections[0].Cues[1].Text);
        Assert.IsNull(Explain.Tour("Weather", "btc-1h", prices, "2023-01-03T00:00"));
    }

    [TestMethod]
    public void Definitions_AreToldInADirection_AndCitedByTheTourAndTheAnalysisAlike()
    {
        Assert.AreEqual("A Weak Downswing always has an HH as its Swing High, because the MSB took the price above the Swing High before it.",
            Definitions.Text("Swing.weak", EnumSwingDirection.Down));
        Assert.AreEqual("A Downswing is characterised by an Upleg from the low to the Swing High, followed by a Downleg from the Swing High to the BoS.",
            Definitions.Text("Swing.legs", EnumSwingDirection.Down));
        CollectionAssert.AreEqual(new[] { "A Swing is a range of positions. It begins at the position that the BoS breaks, and ends at the BoS itself.", "Until the BoS happens it is only a Candidate Swing. The BoS Confirms it." },
            Definitions.Of(EnumAnnotationType.Downswing).Take(2).ToArray(), "A term page lists its family's definitions.");
        Assert.AreEqual("After an MSB against a Trend, the next BoS decides. An Upswing's BoS continues the Uptrend with a Weak Upswing. A Downswing's BoS ends it.",
            Definitions.Text("Trend.next", EnumSwingDirection.Up));
        Assert.AreEqual("A candle shows a whole four hours of trading in one shape, drawn from four prices.", Definitions.Text("Candle.what", period: "four hours"));
        Assert.IsFalse(Definitions.NeedsDirection("Candle.what"), "What a candle spans is not a direction.");
        Assert.IsTrue(Definitions.NeedsDirection("MSB.before"));
        Assert.AreEqual("An MSB refers to the prior Swing High, and so can be observed before a new Swing is Confirmed.",
            Definitions.Of(EnumAnnotationType.BullishMarketStructureBreak).Single(x => x.StartsWith("An MSB refers")), "A Bullish MSB is told against a Downswing.");
        Assert.AreEqual("A BoS closes beyond the position its Swing begins at.", Definitions.Of(EnumAnnotationType.BearishBreakOfStructure)[0], "A BoS lists the Swing's rule for it first.");
        foreach (var term in Gradient.CryptoAnalysis.Terms.All)
            Assert.AreNotEqual(0, Definitions.Of(term.Type).Count, $"{term.Type} has definitions.");

        // A cue that cites a definition has it as its text, written out in the expanded tour for the narration.
        var def = JsonNode.Parse("""{ "dataset": "btc-1h", "start": { "time": "2023-01-01T00:00" }, "sections": [{ "cues": [{ "define": "Trend.ends" }, { "define": "Trend.next" }, { "define": "Trend.nothing" }] }] }""")!;
        var tour = Tours.Compile(def, TourPrices.Value);
        Assert.AreEqual("A Trend ends at the BoS of the first Swing in the opposite direction.", tour.Sections[0].Cues[0].Text);
        Assert.AreEqual("A Trend ends at the BoS of the first Swing in the opposite direction.", tour.Expanded!["sections"]![0]!["cues"]![0]!["texts"]!["education"]!.GetValue<string>());
        CollectionAssert.AreEqual(new[]
        {
            "section 1, text 2: the definition \"Trend.next\" is told in a direction: \"direction\": \"Up\" or \"Down\"",
            "section 1, text 3: no definition \"Trend.nothing\" in definitions.json",
        }, tour.Errors.Where(x => x.Contains("definition")).ToList());

        // The analysis tells the same: a Trend's explanation is its definitions.
        var trend = Explain.Trend(TourPrices.Value, 90, level: 1, reached: 90, out _)!;
        CollectionAssert.IsSubsetOf(new[] { Definitions.Text("Trend.ends", EnumSwingDirection.Up), Definitions.Text("Swing.weak", EnumSwingDirection.Up) }, Texts(trend, EnumDetail.Education));
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
