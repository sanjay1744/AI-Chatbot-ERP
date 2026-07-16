using System;
using System.IO;
using System.Linq;
using UglyToad.PdfPig;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;

namespace TestParser
{
    class Program
    {
        static void Main(string[] args)
        {
            string filePath = @"d:\AriyAI\chatbot_\backend\AriyAI.ERP.Api\uploads\attachments\dd4adf37-902c-4964-9815-9a34a52072d0_Enquiry_Acknowledgement_TEST.pdf";
            if (!File.Exists(filePath))
            {
                Console.WriteLine("File not found");
                return;
            }

            using (var pdf = PdfDocument.Open(filePath))
            {
                foreach (var page in pdf.GetPages())
                {
                    Console.WriteLine("\n=== METHOD A: ContentOrderTextExtractor.GetText(page, true) ===");
                    try
                    {
                        var text = ContentOrderTextExtractor.GetText(page, true);
                        Console.WriteLine(text);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }

                    Console.WriteLine("\n=== METHOD B: BoundingBox Grouping (Words by Y coordinate) ===");
                    try
                    {
                        // Group words by vertical bottom coordinate, allowing a small tolerance of 3 points for slightly misaligned text
                        var words = page.GetWords();
                        var groupedLines = words
                            .GroupBy(w => Math.Round(w.BoundingBox.Bottom, 1)) // round to 1 decimal place or group by close Y coordinates
                            .OrderByDescending(g => g.Key);

                        // Better Y coordinate clustering (grouping words whose vertical centers or bottoms are within 3 points of each other)
                        var sortedWords = words.OrderByDescending(w => w.BoundingBox.Bottom).ToList();
                        var linesList = new System.Collections.Generic.List<System.Collections.Generic.List<UglyToad.PdfPig.Content.Word>>();
                        
                        foreach (var word in sortedWords)
                        {
                            bool added = false;
                            foreach (var line in linesList)
                            {
                                // If the word is vertically close to the average Y coordinate of this line, group it
                                double avgBottom = line.Average(w => w.BoundingBox.Bottom);
                                if (Math.Abs(word.BoundingBox.Bottom - avgBottom) < 4)
                                {
                                    line.Add(word);
                                    added = true;
                                    break;
                                }
                            }
                            if (!added)
                            {
                                linesList.Add(new System.Collections.Generic.List<UglyToad.PdfPig.Content.Word> { word });
                            }
                        }

                        foreach (var line in linesList)
                        {
                            var orderedLineWords = line.OrderBy(w => w.BoundingBox.Left);
                            Console.WriteLine(string.Join(" ", orderedLineWords.Select(w => w.Text)));
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Error: " + ex.Message);
                    }
                    Console.WriteLine("------------------------------------------------------------------");
                }
            }
        }
    }
}
