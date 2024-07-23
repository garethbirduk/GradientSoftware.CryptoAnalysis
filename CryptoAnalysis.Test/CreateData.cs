namespace Gradient.CryptoAnalysis.Tests
{
    public static class TestHelper
    {
        public static List<Price> CreatePriceData(DateTime startDateTime, int interval, int count)
        {
            var random = new Random();
            var data = new List<Price>();

            double lastClose = random.Next(100, 200); // Initial close price

            for (int i = 0; i < count; i++)
            {
                double open = lastClose;
                double close = open + random.Next(-10, 11); // Random variation
                double high = Math.Max(open, close) + random.Next(1, 5);
                double low = Math.Min(open, close) - random.Next(1, 5);

                var cryptoData = new Price
                {
                    Time = startDateTime.AddMinutes(i * interval),
                    Open = open,
                    Close = close,
                    High = high,
                    Low = low
                };

                data.Add(cryptoData);
                lastClose = close; // Set lastClose for the next iteration
            }

            return data;
        }
    }
}