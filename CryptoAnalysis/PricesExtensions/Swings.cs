namespace Gradient.CryptoAnalysis
{
    public static partial class Segments
    {
        public static List<Upswing> ToSwings(this List<Price> prices)
        {
            var swings = new List<Upswing>();
            if (prices.Count() == 0)
                return swings;

            var segments = prices.ToSegments();
            foreach (var segment in segments.Where(x => x.Count() > 1))
            {
                Price? next = null;
                if (segment.Last() != prices.Last())
                    next = prices[prices.IndexOf(segment.Last()) + 1];

                var swing = new Upswing(segment, swings.LastOrDefault(), next);
                swings.Add(swing);
            }

            return swings;
        }
    }
}