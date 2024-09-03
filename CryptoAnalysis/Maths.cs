namespace CryptoAnalysis
{
    public static class Maths
    {
        public static double PercentageDecrease(double initial, double final)
        {
            return -PercentageIncrease(initial, final);
        }

        public static double PercentageIncrease(double initial, double final)
        {
            if (initial == 0)
            {
                // If initial is zero, return +100% for positive final, -100% for negative final
                return final > 0 ? 100 : -100;
            }

            return 100 * (final - initial) / initial;
        }
    }
}