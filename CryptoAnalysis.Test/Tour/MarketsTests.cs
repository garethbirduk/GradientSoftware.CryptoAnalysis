using System.Text.Json;
using Gradient.CryptoAnalysis.Site;
using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Test.Tour;

[TestClass]
public class MarketsTests
{
    private static readonly string RepoRoot = FindRepoRoot(AppContext.BaseDirectory);
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);



    [TestMethod]
    public void FromLondon_IsAnHourBackInSummerTimeOnly()
    {
        Assert.AreEqual(new DateTime(2021, 7, 1, 12, 0, 0, DateTimeKind.Utc), Markets.FromLondon(new DateTime(2021, 7, 1, 13, 0, 0, DateTimeKind.Utc)));
        Assert.AreEqual(new DateTime(2021, 1, 1, 13, 0, 0, DateTimeKind.Utc), Markets.FromLondon(new DateTime(2021, 1, 1, 13, 0, 0, DateTimeKind.Utc)));
    }

    [TestMethod]
    public void Load_BtcIsOneRunOfHoursFromCoinbasesFirst()
    {
        var prices = Btc.Hourly;
        var steps = prices.Zip(prices.Skip(1), (a, b) => b.DateTime - a.DateTime).ToList();

        Assert.AreEqual(new DateTime(2015, 7, 20, 21, 0, 0, DateTimeKind.Utc), prices[0].DateTime);
        Assert.IsTrue(prices[^1].DateTime >= new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc));
        Assert.IsTrue(steps.All(x => x > TimeSpan.Zero), "Candles out of order or repeated.");
        // The odd hours missing, where the exchange has no candle, but few in all and none long.
        Assert.IsTrue(steps.Count(x => x > TimeSpan.FromHours(1)) < 40);
        Assert.IsTrue(steps.All(x => x <= TimeSpan.FromHours(24)));
    }

    [TestMethod]
    public void Load_TheFourHourMarketIsBuiltFromTheOneSource()
    {
        var fourHour = Markets.Load(Markets.All.Single(x => x.Id == "btc-coinbase-4h"), Btc.RepoRoot);
        var day = new DateTime(2023, 2, 14, 0, 0, 0, DateTimeKind.Utc);
        var hours = Btc.Between(day.AddHours(8), day.AddHours(12));
        var candle = fourHour.Single(x => x.DateTime == day.AddHours(8));

        Assert.AreEqual((hours[0].Open, hours.Max(x => x.High), hours.Min(x => x.Low), hours[^1].Close), (candle.Open, candle.High, candle.Low, candle.Close));
        Assert.AreEqual(0, Markets.All.Count(x => x.Id.Contains("bitstamp")), "Bitstamp's BTC is put away, in Markets.Archived.");
    }

    [TestMethod]
    public void Years_AreThoseAWindowsCandlesFallIn()
    {
        var prices = Enumerable.Range(0, 48).Select(i => new Price { DateTime = Start.AddHours(-24 + i) }).ToList();

        CollectionAssert.AreEqual(new[] { 2023, 2024 }, Markets.Years(prices, (0, 48)).ToList());
        CollectionAssert.AreEqual(new[] { 2024 }, Markets.Years(prices, (24, 48)).ToList());
        Assert.AreEqual(0, Markets.Years(prices, (5, 5)).Count());
    }

    [TestMethod]
    public void PeriodRoles_LockedRefusesTestIsLoggedAndSweepsKeepToSearch()
    {
        var dir = Directory.CreateTempSubdirectory();
        try
        {
            var roles = new PeriodRoles(Path.Combine(dir.FullName, "periods.json"), Path.Combine(dir.FullName, "log", "periods.log.jsonl"));
            var prices = Enumerable.Range(0, 48).Select(i => new Price { DateTime = Start.AddHours(-24 + i) }).ToList();
            var both = (0, 48);

            Assert.AreEqual(EnumPeriodRole.Search, roles.Role("btc-coinbase", 2023));
            Assert.IsNull(roles.Check("btc-coinbase-1h", prices, both, "sweep", "s"));

            roles.Set("btc-coinbase", 2024, EnumPeriodRole.Test);
            StringAssert.Contains(roles.Check("btc-coinbase-4h", prices, both, "sweep", "s"), "Search");
            StringAssert.Contains(roles.Check("btc-coinbase-1h", prices, both, "explore", "s"), "Search");
            Assert.IsNull(roles.Check("btc-coinbase-1h", prices, both, "baseline", "s"));
            // The same market at another candle length shares its periods, so a run there is a look at them too.
            Assert.IsNull(roles.Check("btc-coinbase-4h", prices, both, "baseline", "s"));

            Assert.IsNull(roles.Check("eth-coinbase-1h", prices, both, "sweep", "s"));
            Assert.AreEqual(2, roles.TestRuns()[("btc-coinbase", 2024)]);

            roles.Set("btc-coinbase", 2024, EnumPeriodRole.Locked);
            StringAssert.Contains(roles.Check("btc-coinbase-1h", prices, both, "run", "s"), "Locked");
            Assert.IsNull(roles.Check("btc-coinbase-1h", prices, (0, 24), "sweep", "s"));

            var log = File.ReadAllLines(Path.Combine(dir.FullName, "log", "periods.log.jsonl")).Select(x => JsonDocument.Parse(x).RootElement.GetProperty("Kind").GetString()).ToList();
            CollectionAssert.AreEqual(new[] { "role", "baseline", "baseline", "role" }, log);
        }
        finally
        {
            dir.Delete(true);
        }
    }

    private static string FindRepoRoot(string start)
    {
        for (var dir = new DirectoryInfo(start); dir != null; dir = dir.Parent)
        {
            if (File.Exists(Path.Combine(dir.FullName, "CryptoAnalysis.sln")))
                return dir.FullName;
        }

        throw new InvalidOperationException("The repository root was not found.");
    }
}
