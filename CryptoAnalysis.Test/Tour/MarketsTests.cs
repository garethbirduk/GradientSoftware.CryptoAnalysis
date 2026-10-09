using System.Text.Json;
using Gradient.CryptoAnalysis.Site;
using Gradient.CryptoAnalysis.Strategies;

namespace Gradient.CryptoAnalysis.Test.Tour;

[TestClass]
public class MarketsTests
{
    private static readonly string RepoRoot = FindRepoRoot(AppContext.BaseDirectory);
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static readonly Lazy<List<Price>> BtcCoinbase = new(() => Markets.Load(Markets.All.Single(x => x.Id == "btc-coinbase-1h"), RepoRoot));

    [TestMethod]
    public void FromLondon_IsAnHourBackInSummerTimeOnly()
    {
        Assert.AreEqual(new DateTime(2021, 7, 1, 12, 0, 0, DateTimeKind.Utc), Markets.FromLondon(new DateTime(2021, 7, 1, 13, 0, 0, DateTimeKind.Utc)));
        Assert.AreEqual(new DateTime(2021, 1, 1, 13, 0, 0, DateTimeKind.Utc), Markets.FromLondon(new DateTime(2021, 1, 1, 13, 0, 0, DateTimeKind.Utc)));
    }

    [TestMethod]
    public void Load_BtcCoinbaseIsOneRunOfHoursFrom2020InUtc()
    {
        var prices = BtcCoinbase.Value;
        var steps = prices.Zip(prices.Skip(1), (a, b) => b.DateTime - a.DateTime).ToList();

        Assert.AreEqual(new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc), prices[0].DateTime);
        Assert.AreEqual(new DateTime(2024, 8, 5, 14, 0, 0, DateTimeKind.Utc), prices[^1].DateTime);
        Assert.IsTrue(steps.All(x => x > TimeSpan.Zero), "Candles out of order or repeated.");
        // The odd hour missing, as where the clocks go back or the exchange paused, but no more than a few in all.
        Assert.IsTrue(steps.Count(x => x > TimeSpan.FromHours(1)) < 20);
        Assert.IsTrue(steps.All(x => x <= TimeSpan.FromHours(6)));
    }

    [TestMethod]
    public void Load_BtcCoinbaseIsTheSameCandleEitherSideOfTheJoin()
    {
        // The 2023 file holds the true UTC times; the 2020 file, read back from London time, must agree with it where both
        // have the candle, as in the summer of 2023.
        var older = new Csv.CsvReaderHelper().ReadData<Price, global::CryptoAnalysis.Csv.ClassMaps.PriceClassMap>(Path.Combine(RepoRoot, "CryptoAnalysis.Test", "TestData", "COINBASE_BTCUSD, 60.csv"))
            .Select(x => (Time: Markets.FromLondon(x.DateTime), x.Close)).Where(x => x.Time.Year == 2023 && x.Time.Month == 7).ToDictionary(x => x.Time, x => x.Close);
        var joined = BtcCoinbase.Value.Where(x => older.ContainsKey(x.DateTime)).ToList();

        Assert.IsTrue(joined.Count > 600);
        Assert.IsTrue(joined.All(x => Math.Abs(older[x.DateTime] - x.Close) < 0.02), "The 2020 file's times are not read back to UTC.");
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
            Assert.IsNull(roles.Check("btc-1h", prices, both, "baseline", "s"));
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
