namespace Gradient.CryptoAnalysis
{
    public static partial class UpswingExtensions
    {
        public static List<Upswing> FinalInterimUpswings(this Upswing upswing, EnumCloseType closeType, int maxDepth, int depth = 0)
        {
            var interminUpswings = upswing.InterimUpswings(closeType, skip: 0);

            while (depth < maxDepth)
            {
                var finalInterimUpswing = interminUpswings.LastOrDefault();
                if (finalInterimUpswing == null)
                    return interminUpswings;
                else
                {
                    var nextDepthInterimUpswings = FinalInterimUpswings(interminUpswings.Last(), closeType, maxDepth, depth++);
                    if (!nextDepthInterimUpswings.Any())
                        return interminUpswings;
                }
            }
            return interminUpswings;
        }
    }
}