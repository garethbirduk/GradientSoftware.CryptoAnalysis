namespace Gradient.CryptoAnalysis;

/// <summary>
/// A number that stands for a stretch of candles, the same each run for the same candles: what is worked out from them and
/// kept on disk is kept against it, and is no longer used once any of them has changed.
/// </summary>
public static class PriceFingerprint
{
    /// <summary>
    /// The fingerprint of the first count candles: their times and their open, high, low and close.
    /// </summary>
    public static ulong Of(IReadOnlyList<Price> prices, int count)
    {
        // FNV-1a, a value at a time.
        var hash = 14695981039346656037UL;
        void Add(long value) => hash = (hash ^ (ulong)value) * 1099511628211UL;
        for (var i = 0; i < count; i++)
        {
            var p = prices[i];
            Add(p.DateTime.Ticks);
            Add(BitConverter.DoubleToInt64Bits(p.Open));
            Add(BitConverter.DoubleToInt64Bits(p.High));
            Add(BitConverter.DoubleToInt64Bits(p.Low));
            Add(BitConverter.DoubleToInt64Bits(p.Close));
        }

        return hash;
    }
}
