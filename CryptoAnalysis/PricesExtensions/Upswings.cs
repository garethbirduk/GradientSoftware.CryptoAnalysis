namespace Gradient.CryptoAnalysis
{
    public static partial class PricesExtensions_Upswings
    {
        public static List<Upswing> ToUpswings(this List<Price> prices, EnumCloseType closeType,
            bool trimStart = false, bool trimEnd = false, int maxSwingSize = 0)
        {
            var swings = new List<Upswing>();
            if (prices.Count() == 0)
                return swings;

            var segments = prices.ToHighSegments(closeType, trimStart, trimEnd);

            Upswing previousSwing = null;
            foreach (var segment in segments.Where(x => x.Count() > 1))
            {
                Price? next = null;
                if (segment.Last() != prices.Last())
                    next = prices[prices.IndexOf(segment.Last()) + 1];

                var swing = new Upswing(segment, previousSwing, next);

                if (swing.MarketStructureBreak == null && swing.BreakOfStructure == null)
                {
                    previousSwing = null;
                }
                else
                {
                    swings.Add(swing);
                    previousSwing = swing;
                }
            }

            if (maxSwingSize > 0)
            {
                while (swings.Count > 0 && swings.Select(x => x.Prices.Count).Max() > maxSwingSize)
                {
                    var swing = swings.Where(x => x.Prices.Count > maxSwingSize).First();

                    var index = swings.IndexOf(swing);
                    var pre = swings.Take(index).ToList();
                    var interim = swing.InterimUpswings(closeType).ToList();
                    var post = swings.Skip(index + 1).ToList();

                    swings = pre.Union(interim).Union(post).ToList();
                }
            }

            return swings;
        }

        public static List<UpwardBreakout> ToUpwardBreakouts(this List<Price> prices, EnumCloseType closeType,
            bool trimStart = false, bool trimEnd = false, int maxSwingSize = 0)
        {
            var upswings = prices.ToUpswings(closeType, trimStart, trimEnd);
            //var higherHighs = upswings.Select(x => x.Prices.First()).ToList();

            var upwardBreakouts = new List<UpwardBreakout>();

            foreach (var upswing in upswings)
            {
                var breakOfStructure = upswing.BreakOfStructure;
                if (breakOfStructure == null)
                    continue;

                if (upswing == upswings.Last())
                    continue;

                var nextUpswing = upswings.Next(upswing);
                if (nextUpswing == null)
                    continue;

                //if (higherHighs.Select(x => x.DateTime).Contains(breakOfStructure.DateTime))
                //    continue;

                //var high = higherHighs.Where(x => x.DateTime > breakOfStructure.DateTime).FirstOrDefault();
                //if (high == null)
                //    continue;

                var upwardBreakoutPrices = prices.Where(x => x.DateTime >= breakOfStructure.DateTime && x.DateTime <= nextUpswing.Prices.First().DateTime).ToList();
                if (upwardBreakoutPrices.Count() > 1)
                {
                    upwardBreakouts.Add(new UpwardBreakout(upwardBreakoutPrices, upswing));
                }
            }

            return upwardBreakouts;
        }
    }
}

public static class ListExtensions
{
    public static T? Next<T>(this List<T> list, T item) where T : class
    {
        if (item == null)
            return default;

        var index = list.IndexOf(item);
        if (index == -1)
            return default;

        if (item.Equals(list.LastOrDefault()))
            return default;

        return list[index + 1];
    }
}