using System.Data;

namespace Gradient.CryptoAnalysis
{
    public static class HighLows
    {
        public static DataTable ConvertToDataTable(List<Price> cryptoData)
        {
            DataTable dataTable = new DataTable();
            dataTable.Columns.Add("time", typeof(DateTime));
            dataTable.Columns.Add("open", typeof(double));
            dataTable.Columns.Add("high", typeof(double));
            dataTable.Columns.Add("low", typeof(double));
            dataTable.Columns.Add("close", typeof(double));

            foreach (var data in cryptoData)
            {
                dataTable.Rows.Add(data.Time, data.Open, data.High, data.Low, data.Close);
            }

            return dataTable;
        }

        public static void PrintPattern(List<Price> Data)
        {
            List<DataRow> sawtoothPattern = new List<DataRow>();
            var dataTable = ConvertToDataTable(Data);
            string currentState = null; // null means we haven't found the first extreme yet

            for (int i = 1; i < dataTable.Rows.Count; i++)
            {
                var currentRow = dataTable.Rows[i];
                var previousRow = dataTable.Rows[i - 1];

                if (currentState == null)
                {
                    if ((double)currentRow["high"] > (double)previousRow["high"])
                    {
                        currentState = "high";
                        sawtoothPattern.Add(currentRow);
                    }
                    else if ((double)currentRow["low"] < (double)previousRow["low"])
                    {
                        currentState = "low";
                        sawtoothPattern.Add(currentRow);
                    }
                }
                else if (currentState == "high")
                {
                    if ((double)currentRow["low"] < (double)previousRow["low"])
                    {
                        currentState = "low";
                        sawtoothPattern.Add(currentRow);
                    }
                }
                else if (currentState == "low")
                {
                    if ((double)currentRow["high"] > (double)previousRow["high"])
                    {
                        currentState = "high";
                        sawtoothPattern.Add(currentRow);
                    }
                }
            }

            // Print the sawtooth pattern
            Console.WriteLine("Sawtooth Pattern:");
            foreach (var row in sawtoothPattern)
            {
                Console.WriteLine($"{row["time"]}, Open: {row["open"]}, High: {row["high"]}, Low: {row["low"]}, Close: {row["close"]}");
            }
        }
    }
}