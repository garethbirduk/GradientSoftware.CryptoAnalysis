using CryptoAnalysis.Csv;
using Gradient.CryptoAnalysis;

namespace CryptoAnalysis.Test.Scenarios
{
    [TestClass]
    public class IndicatorTests
    {
        public static readonly string _cryptoDataFilePath = Path.Combine("TestData", "Scenarios", "Indicators", "COINBASE_BTCUSD, 60.csv");

        public List<Price> _prices { get; private set; }

        [TestInitialize]
        public void TestInitialize()
        {
            _prices = new CsvReaderHelper().ReadData<Price, PriceClassMap>(_cryptoDataFilePath).ToList();
        }

        [TestMethod]
        public void TestIsBreakOfStructureCondition()
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

        [TestMethod]
        public void TestIsMarketStructureBreakCondition()
        {
            var positionRules = new ConditionRules();

            positionRules.PreConditions.AndConditions.Add(new IsMarketStructureBreakCondition(0));

            var check = new CheckIndicators()
            {
                PositionRules = positionRules,
                Prices = _prices.ToList(),
                OutputFilepath = Path.Combine("c:\\", "temp", "output", $"IsMarketStructureBreakCondition.csv")
            };
            check.Execute();
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

        [DataTestMethod]
        [DataRow(5, 0)]
        [DataRow(5, 1)]
        [DataRow(5, 2)]
        [DataRow(10, 0)]
        [DataRow(10, 1)]
        [DataRow(15, 2)]
        public void TestIsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition(int minSwings, int maxMsb)
        {
            var positionRules = new ConditionRules();

            for (minSwings = 10; minSwings < 20; minSwings++)
            {
                for (maxMsb = 0; maxMsb < 3; maxMsb++)
                {
                    positionRules.PreConditions.AndConditions.Add(new IsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition(0, minSwings, maxMsb));

                    var check = new CheckIndicators()
                    {
                        PositionRules = positionRules,
                        Prices = _prices,
                        //Prices = _prices.Where(x => x.DateTime > new DateTime(2024, 07, 12)).ToList(),
                        OutputFilepath = Path.Combine("c:\\", "temp", "output", $"IsSuccessiveUpswingsWithMaxMarketStructureBreaksCondition_{minSwings}_{maxMsb}.csv")
                    };
                    check.Execute();
                }
            }
        }
    }
}