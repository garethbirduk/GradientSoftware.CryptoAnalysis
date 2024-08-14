namespace Gradient.CryptoAnalysis
{
    public static partial class Segments
    {
        public static List<List<Price>> ToSegments(this List<Price> prices)
        {
            if (!prices.Any())
                return new List<List<Price>>();

            var highs = prices.HighCloses();

            var segments = new List<List<Price>>();

            for (int i = 0; i < highs.Count; i++)
            {
                var high = highs[i];
                var startIndex = prices.IndexOf(high);

                var endIndex = prices.IndexOf(prices.Last()) + 1;
                if (high != highs.Last())
                {
                    endIndex = prices.IndexOf(highs[i + 1]);
                }

                var skip = startIndex;
                var take = endIndex - startIndex;

                var segment = prices.Skip(skip).Take(take).ToList();
                if (segment.Count() > 1)
                    segments.Add(segment);
            }

            return segments;
        }
    }
}