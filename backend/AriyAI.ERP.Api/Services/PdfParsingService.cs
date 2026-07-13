using System;
using System.IO;
using System.Text;
using UglyToad.PdfPig;

namespace AriyAI.ERP.Api.Services
{
    public class PdfParsingService
    {
        public string ParsePdfText(string filePath)
        {
            if (!File.Exists(filePath)) return string.Empty;

            var sb = new StringBuilder();
            try
            {
                using (var pdf = PdfDocument.Open(filePath))
                {
                    foreach (var page in pdf.GetPages())
                    {
                        string pageText = UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor.ContentOrderTextExtractor.GetText(page, true);
                        if (!string.IsNullOrEmpty(pageText))
                        {
                            sb.AppendLine(pageText);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PDF parse error: {ex.Message}");
            }

            return sb.ToString();
        }
    }
}
