namespace Gradient.CryptoAnalysis
{
    public static partial class PricesExtensions_Downswings
    {
        public static List<Downswing> ToDownswings(this List<Price> prices, EnumCloseType closeType,
            bool trimStart = false, bool trimEnd = false, int maxSwingSize = 0)
        {
            var swings = new List<Downswing>();
            if (prices.Count() == 0)
                return swings;

            var segments = prices.ToLowSegments(closeType, trimStart);

            Downswing previousSwing = null;
            foreach (var segment in segments.Where(x => x.Count() > 1))
            {
                Price? next = null;
                if (segment.Last() != prices.Last())
                    next = prices[prices.IndexOf(segment.Last()) + 1];

                var swing = new Downswing(segment, previousSwing, next);

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
                    var interim = swing.InterimDownswings(EnumCloseType.Close).ToList();
                    var post = swings.Skip(index + 1).ToList();

                    swings = pre.Union(interim).Union(post).ToList();
                }
            }

            return swings;
        }
    }
}