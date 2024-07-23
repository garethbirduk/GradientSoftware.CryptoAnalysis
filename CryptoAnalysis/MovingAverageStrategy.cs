using System.Collections.Generic;
using System.Linq;

namespace Gradient.CryptoAnalysis
{
    public class MovingAverageStrategy
    {
        private int _longPeriod;
        private List<double> _longWindow = new List<double>();
        private int _shortPeriod;
        private List<double> _shortWindow = new List<double>();

        public MovingAverageStrategy(int shortPeriod, int longPeriod)
        {
            _shortPeriod = shortPeriod;
            _longPeriod = longPeriod;
        }

        public string Evaluate(Price data)
        {
            _shortWindow.Add(data.Close);
            _longWindow.Add(data.Close);

            if (_shortWindow.Count > _shortPeriod)
                _shortWindow.RemoveAt(0);
            if (_longWindow.Count > _longPeriod)
                _longWindow.RemoveAt(0);

            if (_shortWindow.Count == _shortPeriod && _longWindow.Count == _longPeriod)
            {
                var shortAverage = _shortWindow.Average();
                var longAverage = _longWindow.Average();

                if (shortAverage > longAverage)
                    return "Buy";
                if (shortAverage < longAverage)
                    return "Sell";
            }

            return "Hold";
        }
    }
}