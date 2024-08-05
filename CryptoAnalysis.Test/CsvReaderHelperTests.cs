using Microsoft.VisualStudio.TestTools.UnitTesting;
using Gradient.CryptoAnalysis;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using CsvHelper;

namespace Gradient.CryptoAnalysis.Tests
{
    [TestClass]
    public class CsvReaderHelperTests
    {
        private string _backtestResultsFilePath;
        private string _cryptoDataFilePath;

        private void CreateTestBacktestResultsFile()
        {
            if (!File.Exists(_backtestResultsFilePath))
            {
                var csvContent =
@"Coin,Entry Date & DateTime,Exit Date & DateTime,Direction,Entry,Stop Loss,Exit,Returns,Win / Loss
BTCUSD,2022-01-01 21:00:00,2022-01-01 22:00:00,Long,47444.00,47301.00,47301.00,-1,Loss
BTCUSD,2022-01-06 16:00:00,2022-01-06 17:00:00,Long,42940.47,42751.26,42751.26,-1,Loss
BTCUSD,2022-01-11 15:00:00,2022-01-11 16:00:00,Long,41645.64,41592.47,42396.73,14.12619898,Win";

                File.WriteAllText(_backtestResultsFilePath, csvContent);
            }
        }

        private void CreateTestCryptoDataFile()
        {
            if (!File.Exists(_cryptoDataFilePath))
            {
                var csvContent =
@"time,open,high,low,close
2024-07-07T07:00:00Z,0.00703,0.007064,0.007,0.007
2024-07-07T08:00:00Z,0.00704,0.007086,0.007001,0.007084
2024-07-07T09:00:00Z,0.007088,0.007141,0.007002,0.007061
2024-07-07T10:00:00Z,0.007064,0.007069,0.007013,0.007013
2024-07-07T11:00:00Z,0.00701,0.007088,0.00701,0.007087";

                File.WriteAllText(_cryptoDataFilePath, csvContent);
            }
        }

        [TestMethod]
        public void CsvReaderHelper_ShouldReadBacktestResultsCorrectly()
        {
            // Arrange
            var csvHelper = new CsvReaderHelper();

            // Act
            var results = csvHelper.ReadBacktestResults(_backtestResultsFilePath).ToList();

            // Assert
            Assert.IsNotNull(results);
            Assert.IsTrue(results.Count > 0);
            Assert.AreEqual("BTCUSD", results[0].Coin);
            var expectedEntryTime = new DateTime(2022, 1, 1, 21, 0, 0);
            var expectedExitTime = new DateTime(2022, 1, 1, 22, 0, 0);
            Assert.AreEqual(expectedEntryTime, results[0].EntryDateTime);
            Assert.AreEqual(expectedExitTime, results[0].ExitDateTime);
            Assert.AreEqual("Long", results[0].Direction);
            Assert.AreEqual(47444.00, results[0].Entry);
            Assert.AreEqual(47301.00, results[0].StopLoss);
            Assert.AreEqual(47301.00, results[0].Exit);
            Assert.AreEqual(-1, results[0].Returns);
            Assert.AreEqual("Loss", results[0].WinLoss);
        }

        [TestMethod]
        public void CsvReaderHelper_ShouldReadCryptoCoinDataCorrectly()
        {
            // Arrange
            var csvHelper = new CsvReaderHelper();

            // Act
            var data = csvHelper.ReadCryptoCoinData(_cryptoDataFilePath).ToList();

            // Assert
            Assert.IsNotNull(data);
            Assert.IsTrue(data.Count > 0);
            var expectedTime = DateTime.SpecifyKind(new DateTime(2024, 7, 7, 7, 0, 0), DateTimeKind.Utc);
            Assert.AreEqual(expectedTime, data[0].DateTime.ToUniversalTime());
            Assert.AreEqual(0.00703, data[0].Open);
            Assert.AreEqual(0.007064, data[0].High);
            Assert.AreEqual(0.007, data[0].Low);
            Assert.AreEqual(0.007, data[0].Close);
        }

        [TestInitialize]
        public void Setup()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            _cryptoDataFilePath = Path.Combine(baseDir, "TestData", "COINBASE_SPAUSD, 60.csv");
            _backtestResultsFilePath = Path.Combine(baseDir, "TestData", "BacktestResults.csv");

            // Create test data files if they don't exist
            CreateTestCryptoDataFile();
            CreateTestBacktestResultsFile();
        }
    }
}