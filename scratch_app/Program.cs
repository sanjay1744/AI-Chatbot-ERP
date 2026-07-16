using System;
using System.IO;
using ExcelDataReader;

class Program
{
    static void Main()
    {
        string excelPath = @"d:\AriyAI\chatbot_\AI_Data\products_table.xlsx";
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        using (var stream = File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        {
            using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
            {
                var result = reader.AsDataSet();
                var table = result.Tables[0];
                Console.WriteLine("Columns count: " + table.Columns.Count);
                for (int c = 0; c < table.Columns.Count; c++)
                {
                    Console.WriteLine($"Col {c}: Name='{table.Columns[c].ColumnName}' | R0='{table.Rows[0][c]}' | R1='{table.Rows[1][c]}' | R2='{table.Rows[2][c]}'");
                }
            }
        }
    }
}
