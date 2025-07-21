namespace Gradient.CryptoAnalysis
{
    public static partial class PricesExtensions_Segments
    {
        public static List<Price> ToHigherHighs(this List<Price> prices)
        {
            var segments = prices.ToHighSegments();
            return segments.Select(x => x.First()).ToList();
        }

        public static List<Price> ToHigherLows(this List<Price> prices)
        {
            var segments = prices.ToHighSegments();
            var list = new List<Price>();
            foreach (var segment in segments)
            {
                var low = segment.Min(x => x.Close);
                list.Add(segment.Where(x => x.Close == low).First());
            }
            return list;
        }

        public static List<List<Price>> ToHighSegments(this List<Price> prices)
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

        public static List<List<Price>> ToLowSegments(this List<Price> prices)
        {
            if (!prices.Any())
                return new List<List<Price>>();

            var lows = prices.LowCloses();

            var segments = new List<List<Price>>();

            for (int i = 0; i < lows.Count; i++)
            {
                var low = lows[i];
                var startIndex = prices.IndexOf(low);

                var endIndex = prices.IndexOf(prices.Last()) + 1;
                if (low != lows.Last())
                {
                    endIndex = prices.IndexOf(lows[i + 1]);
                }

                var skip = startIndex;
                var take = endIndex - startIndex;

                var segment = prices.Skip(skip).Take(take).ToList();
                if (segment.Count() > 1)
                    segments.Add(segment);
            }

            return segments;
        }

        public static List<Tuple<Price, Price>> ToUptrendBreakOfStructures(this List<Price> prices)
        {
            var list = new List<Tuple<Price, Price>>();
            var higherHighs = prices.ToHigherHighs();

            var pIndex = 0;
            var hhIndex = 0;
            while (pIndex < prices.Count && hhIndex < higherHighs.Count)
            {
                var price = prices[pIndex];
                if (price.Close > higherHighs[hhIndex].Close)
                {
                    list.Add(new(price, higherHighs[hhIndex]));
                    hhIndex++;
                }
                pIndex++;
            }

            return list;
        }

        public static List<Price> ToUptrendSawtooth(this List<Price> prices)
        {
            return prices.ToHigherHighs().Union(prices.ToHigherLows()).OrderBy(x => x.DateTime).ToList();
        }

        //public static List<List<Price>> ToHighSegmentUsingHighs(this List<Price> prices)
        //{
        //    if (!prices.Any())
        //        return new List<List<Price>>();

        //    var highs = prices.HighHighs();

        //    var segments = new List<List<Price>>();

        //    for (int i = 0; i < highs.Count; i++)
        //    {
        //        var high = highs[i];
        //        var startIndex = prices.IndexOf(high);

        //        var endIndex = prices.IndexOf(prices.Last()) + 1;
        //        if (high != highs.Last())
        //        {
        //            endIndex = prices.IndexOf(highs[i + 1]);
        //        }

        //        var skip = startIndex;
        //        var take = endIndex - startIndex;

        //        var segment = prices.Skip(skip).Take(take).ToList();
        //        if (segment.Count() > 1)
        //            segments.Add(segment);
        //    }

        //    return segments;
        //}
    }
}