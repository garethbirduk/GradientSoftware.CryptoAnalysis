namespace Gradient.CryptoAnalysis.Test.Site;

/// <summary>
/// A store written out and read back is the store it was.
/// </summary>
[TestClass]
public class StructureStoreKeptTests
{
    private static string Json<T>(T value) => System.Text.Json.JsonSerializer.Serialize(value);

    private static string Describe(StructureStore store, int at) => string.Join("\n",
        Json(store.Pivots(0, at, at)), Json(store.Swings(0, at, at)), Json(store.Breaks(0, at, at)), Json(store.Trends(0, at, at)),
        Json(store.Retracements(0, at, at)), Json(store.Candidates(at)), Json(store.Ranges(0, at, at)));

    [TestMethod]
    public void AStoreReadBack_HasWhatItHad_AtEveryCandleAskedOf()
    {
        var prices = Btc.From2023().GetRange(0, 3000);
        var store = StructureStore.Read(prices);
        using var kept = new MemoryStream();
        store.Save(kept);
        kept.Position = 0;

        var back = StructureStore.Load(kept, prices);

        Assert.IsNotNull(back);
        Assert.AreEqual(3000, back.Candles);
        foreach (var at in new[] { 0, 1, 57, 500, 1234, 2000, 2650, 2999 })
            Assert.AreEqual(Describe(store, at), Describe(back, at), $"As known at candle {at}.");
        Assert.AreEqual(Json(MarketStructure.Timeline(store, 8, 1499, 1550, 756)), Json(MarketStructure.Timeline(back, 8, 1499, 1550, 756)), "A batch of the replay.");
        Assert.AreEqual(Json(MarketStructure.Complete(store, 8, 2000, 2500, 5)), Json(MarketStructure.Complete(back, 8, 2000, 2500, 5)), "A stretch of the whole.");
        Assert.IsTrue(store.Ranges(0, 2999).Count > 0 && store.Candidates().Count > 0);
    }

    [TestMethod]
    public void WhatIsNotAStoreOfTheCandles_IsNotReadBack()
    {
        var prices = Btc.From2023().GetRange(0, 600);
        using var kept = new MemoryStream();
        StructureStore.Read(prices).Save(kept);

        Assert.IsNull(StructureStore.Load(new MemoryStream(kept.ToArray()), prices.GetRange(0, 599)), "Other candles than it was read from.");
        Assert.IsNull(StructureStore.Load(new MemoryStream(kept.ToArray()), prices, EnumPriceBasis.Wick), "Another basis.");
        Assert.IsNull(StructureStore.Load(new MemoryStream(kept.ToArray()[..(int)(kept.Length / 2)]), prices), "Half of one.");
        Assert.IsNull(StructureStore.Load(new MemoryStream([1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12]), prices), "Something else.");
        Assert.IsNotNull(StructureStore.Load(new MemoryStream(kept.ToArray()), prices));
    }
}
