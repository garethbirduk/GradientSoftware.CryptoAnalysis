using CryptoAnalysis.Csv;
using Gradient.CryptoAnalysis;

namespace CryptoAnalysis.Test.Scenarios
{
    [TestClass]
    public class IndicatorTests
    {
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "Scenarios", "Indicators", "COINBASE_BTCUSD (2024), 60.csv");

        public List<Price> _prices { get; private set; }

        [TestMethod]
        public void TestBreakOfStructure()
        {
            var positionRules = new ConditionRules();

            positionRules.PreConditions.AndConditions.Add(new IsBreakOfStructureCondition(0));

            var check = new CheckIndicators()
            {
                PositionRules = positionRules,
                Prices = _prices.ToList(),
                OutputFilepath = Path.Combine("c:\\", "temp", "output", $"IsBreakOfStructureCondition.csv")
            };
            check.Execute();
        }

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }

        [TestMethod]
        public void TestIsMichaelsEmaGreenCondition()
        {
            var positionRules = new ConditionRules();

            positionRules.PreConditions.AndConditions.Add(new IsMichaelsEmaGreenCondition());

            var check = new CheckIndicators()
            {
                PositionRules = positionRules,
                Prices = _prices,
                OutputFilepath = Path.Combine("c:\\", "temp", "output", $"TestIsMichaelsEmaGreenCondition.csv")
            };
            check.Execute();
        }

        [DataTestMethod]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        public void TestIsSuccessiveGreenCandlesCondition(int successive)
        {
            var positionRules = new ConditionRules();

            positionRules.PreConditions.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(successive));

            var check = new CheckIndicators()
            {
                PositionRules = positionRules,
                Prices = _prices,
                OutputFilepath = Path.Combine("c:\\", "temp", "output", $"IsSuccessiveGreenCandlesCondition{successive}.csv")
            };
            check.Execute();
        }

        [DataTestMethod]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        public void TestIsSuccessiveGreenCandlesConditionWithMichaelsEma(int successive)
        {
            var positionRules = new ConditionRules();

            positionRules.PreConditions.AndConditions.Add(new IsSuccessiveGreenCandlesCondition(successive));
            positionRules.PreConditions.AndConditions.Add(new IsMichaelsEmaGreenCondition());

            var check = new CheckIndicators()
            {
                PositionRules = positionRules,
                Prices = _prices,
                OutputFilepath = Path.Combine("c:\\", "temp", "output", $"IsSuccessiveGreenCandlesConditionWithMichaelsEma{successive}.csv")
            };
            check.Execute();
        }

        [DataTestMethod]
        [DataRow(2)]
        [DataRow(3)]
        [DataRow(4)]
        [DataRow(5)]
        [DataRow(6)]
        public void TestIsSuccessiveRedCandlesConditionWithMichaelsEma(int successive)
        {
            var positionRules = new ConditionRules();

            positionRules.PreConditions.AndConditions.Add(new IsSuccessiveRedCandlesCondition(successive));
            positionRules.PreConditions.AndConditions.Add(new IsMichaelsEmaGreenCondition());

            var check = new CheckIndicators()
            {
                PositionRules = positionRules,
                Prices = _prices,
                OutputFilepath = Path.Combine("c:\\", "temp", "output", $"IsSuccessiveRedCandlesConditionWithMichaelsEma{successive}.csv")
            };
            check.Execute();
        }
    }
}