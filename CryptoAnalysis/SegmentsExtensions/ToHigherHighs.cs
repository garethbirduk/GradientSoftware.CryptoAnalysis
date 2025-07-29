namespace Gradient.CryptoAnalysis
{
    public static partial class SegmentsExtensions
    {
        public static List<Price> ToHigherHighs(this List<List<Price>> segments)
        {
            var list = new List<Price>();

            var initial = segments.FirstOrDefault();
            if (initial == null)
                return list;

            foreach (var segment in segments)
            {
                list.Add(segment.HighClosesIsGreen(EnumCloseType.High).First());
            }
            return list;
        }
    }
}