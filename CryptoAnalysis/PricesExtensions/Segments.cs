namespace Gradient.CryptoAnalysis
{
    public static partial class PricesExtensions_Segments
    {
        public static List<Price> HigherHighs(this List<Price> prices)
        {
            var segments = prices.ToHighSegments(EnumCloseType.Close);
            return segments.Select(x => x.First()).ToList();
        }

        public static List<Price> ToHigherLows(this List<Price> prices)
        {
            var segments = prices.ToHighSegments(EnumCloseType.Close);
            var list = new List<Price>();
            foreach (var segment in segments)
            {
                var low = segment.Min(x => x.Close);
                list.Add(segment.Where(x => x.Close == low).First());
            }
            return list;
        }

        public static List<List<Price>> ToHighSegments(this List<Price> prices, EnumCloseType closeType, bool trimStart = false, bool trimEnd = false)
        {
            if (!prices.Any())
                return new List<List<Price>>();

            var highs = prices.HighClosesIsGreen(closeType);

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

            if (trimStart && segments.Any())
            {
                var segment = segments.First();
                var lowPrice = segment.MinBy(x => x.CloseValue(closeType));
                if (closeType == EnumCloseType.High)
                    lowPrice = segment.MinBy(x => x.High);

                if (lowPrice != null)
                {
                    var highPrice = segment.Where(x => x.DateTime > lowPrice.DateTime).MaxBy(x => x.Close);
                    if (closeType == EnumCloseType.High)
                        highPrice = segment.Where(x => x.DateTime > lowPrice.DateTime).MaxBy(x => x.High);

                    if (highPrice != null)
                        segment.RemoveAll(x => x.DateTime < highPrice.DateTime);
                }
            }

            if (trimEnd && segments.Any())
            {
                if (segments.Last().Last().CloseValue(closeType) < segments.First().First().CloseValue(closeType))
                    segments.Remove(segments.Last());
            }

            return segments;
        }

        public static List<List<Price>> ToLowSegments(this List<Price> prices, EnumCloseType closeType, bool TrimStart = false)
        {
            if (!prices.Any())
                return new List<List<Price>>();

            var Lows = prices.LowClosesIsRed(closeType);
            var Lows2 = prices.AllTimeLows(closeType);

            var segments = new List<List<Price>>();

            for (int i = 0; i < Lows.Count; i++)
            {
                var Low = Lows[i];
                var startIndex = prices.IndexOf(Low);

                var endIndex = prices.IndexOf(prices.Last()) + 1;
                if (Low != Lows.Last())
                {
                    endIndex = prices.IndexOf(Lows[i + 1]);
                }

                var skip = startIndex;
                var take = endIndex - startIndex;

                var segment = prices.Skip(skip).Take(take).ToList();
                if (segment.Count() > 1)
                    segments.Add(segment);
            }

            if (TrimStart && segments.Any())
            {
                var segment = segments.First();
                var lowPrice = segment.MinBy(x => x.Close);
                if (closeType == EnumCloseType.Low)
                    lowPrice = segment.MinBy(x => x.Low);

                if (lowPrice != null)
                {
                    var LowPrice = segment.Where(x => x.DateTime > lowPrice.DateTime).MaxBy(x => x.Close);
                    if (closeType == EnumCloseType.Low)
                        LowPrice = segment.Where(x => x.DateTime > lowPrice.DateTime).MaxBy(x => x.Low);

                    if (LowPrice != null)
                        segment.RemoveAll(x => x.DateTime < LowPrice.DateTime);
                }
            }

            return segments;
        }

        public static (List<Tuple<Price, Price>> breaksOfStructures, List<Tuple<Price, Price>> marketStructureBreaks) ToStructures(this List<Price> prices)
        {
            var breaksOfStructures = new List<Tuple<Price, Price>>();
            var marketStructureBreaks = new List<Tuple<Price, Price>>();
            var sawtooth = prices.ToUptrendSawtooth();
            var swings = prices.ToUpswings(EnumCloseType.High);

            for (var index = 0; index < sawtooth.Count; index += 1)
            {
                if (index + 2 < sawtooth.Count)
                {
                    var start = sawtooth[index];
                    var mid = sawtooth[index + 1];
                    var end = sawtooth[index + 2];

                    var swing = prices.Where(x => x.DateTime >= start.DateTime && x.DateTime <= end.DateTime).ToList();
                }
            }

            var pIndex = 0;
            var hhIndex = 1; // skip first
            while (pIndex < prices.Count && hhIndex < sawtooth.Count - 2)
            {
                var price = prices[pIndex];
                var hl = sawtooth[hhIndex];
                var hh = sawtooth[hhIndex + 1];

                if (price.Close > hh.Close)
                {
                    breaksOfStructures.Add(new(price, hh));
                    hhIndex += 2;
                }

                if (price.Close < hl.Close)
                {
                    marketStructureBreaks.Add(new(price, hh));
                    hhIndex += 2;
                }
                pIndex++;
            }

            return (breaksOfStructures, marketStructureBreaks);
        }

        public static List<Tuple<Price, Price>> ToUptrendMarketStructureBreaks(this List<Price> prices)
        {
            var list = new List<Tuple<Price, Price>>();
            var higherHighs = prices.HigherHighs();

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
            return prices.HigherHighs().Union(prices.ToHigherLows()).OrderBy(x => x.DateTime).ToList();
        }

        //public static List<List<Price>> ToHighSegmentUsingHighs(this List<Price> Prices)
        //{
        //    if (!Prices.Any())
        //        return new List<List<Price>>();

        //    var highs = Prices.HighHighs();

        //    var segments = new List<List<Price>>();

        //    for (int i = 0; i < highs.Count; i++)
        //    {
        //        var high = highs[i];
        //        var startIndex = Prices.IndexOf(high);

        //        var endIndex = Prices.IndexOf(Prices.Last()) + 1;
        //        if (high != highs.Last())
        //        {
        //            endIndex = Prices.IndexOf(highs[i + 1]);
        //        }

        //        var skip = startIndex;
        //        var take = endIndex - startIndex;

        //        var segment = Prices.Skip(skip).Take(take).ToList();
        //        if (segment.Count() > 1)
        //            segments.Add(segment);
        //    }

        //    return segments;
        //}
    }
}