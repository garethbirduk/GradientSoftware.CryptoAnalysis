using Gradient.CryptoAnalysis.Site;

namespace Gradient.CryptoAnalysis.Test.Site;

[TestClass]
public class StructureCacheTests
{
    private static readonly DateTime Start = new(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

    private static List<Price> Closes(int count) =>
        Enumerable.Range(0, count).Select(i => new Price { DateTime = Start.AddHours(i), Open = 100 + i, High = 101 + i, Low = 99 + i, Close = 100.5 + i }).ToList();

    [TestMethod]
    public void Text_IsMadeOnceAndReadBackByAnotherCacheOfTheFolder()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid():N}");
        try
        {
            var made = 0;
            var text = new StructureCache(root).Text(Path.Combine("replay", "a", "0-50.json"), () => { made++; return "{\"first\":\"é\"}"; });
            var again = new StructureCache(root).Text(Path.Combine("replay", "a", "0-50.json"), () => { made++; return "another"; });

            Assert.AreEqual(("{\"first\":\"é\"}", "{\"first\":\"é\"}", 1), (text, again, made));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void Prune_RemovesOnlyOtherBuildsLeftUnwritten()
    {
        var root = Path.Combine(Path.GetTempPath(), $"cache-{Guid.NewGuid():N}");
        try
        {
            var cache = new StructureCache(root);
            cache.Text("kept.json", () => "kept");
            var (old, recent) = (Path.Combine(root, "old-build"), Path.Combine(root, "recent-build"));
            foreach (var folder in new[] { old, recent })
            {
                Directory.CreateDirectory(folder);
                File.WriteAllText(Path.Combine(folder, "x.json.gz"), "x");
            }

            var longAgo = DateTime.UtcNow - StructureCache.KeptFor - TimeSpan.FromDays(1);
            File.SetLastWriteTimeUtc(Path.Combine(old, "x.json.gz"), longAgo);
            Directory.SetLastWriteTimeUtc(old, longAgo);

            cache.Prune();

            Assert.AreEqual((false, true, true), (Directory.Exists(old), Directory.Exists(recent), Directory.Exists(cache.Folder)));
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    [TestMethod]
    public void BatchName_StaysAsCandlesAreAddedAfterItAndChangesWithACandleBeforeIt()
    {
        var settings = (Retracement: 75.0, Band: 20.0);
        var name = ReplayServer.BatchName(new Dataset("d", "D", Closes(300)), 0, 100, settings);

        Assert.AreEqual(name, ReplayServer.BatchName(new Dataset("d", "D", Closes(400)), 0, 100, settings), "Candles after the batch do not change it.");

        var changed = Closes(300);
        changed[20].Close += 1;
        Assert.AreNotEqual(name, ReplayServer.BatchName(new Dataset("d", "D", changed), 0, 100, settings), "A candle before it does.");
        Assert.AreNotEqual(name, ReplayServer.BatchName(new Dataset("d", "D", Closes(300)), 0, 100, (74.0, 20.0)), "So does a range setting.");
        // The last batch of a dataset is of fewer candles until more are added, and is then another batch.
        Assert.AreNotEqual(ReplayServer.BatchName(new Dataset("d", "D", Closes(320)), 0, 300, settings), ReplayServer.BatchName(new Dataset("d", "D", Closes(340)), 0, 300, settings));
    }
}
