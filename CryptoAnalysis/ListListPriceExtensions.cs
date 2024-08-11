namespace Gradient.CryptoAnalysis
{
    public static class ListListPriceExtensions
    {
        public static List<List<Price>> ConcatenateSegments(this List<List<Price>> segments)
        {
            var list = new List<List<Price>>()
            {
                segments.First()
            };

            foreach (var segment in segments.Skip(1))
            {
                if (false && segment.Count < 2)
                {
                    list.Last().AddRange(segment);
                }
                else
                {
                    list.Add(segment);
                }
            }
            return list;
        }
    }
}