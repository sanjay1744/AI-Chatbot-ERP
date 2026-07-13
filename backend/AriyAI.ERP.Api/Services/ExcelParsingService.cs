using System;
using System.IO;
using System.Data;
using System.Linq;
using System.Text.RegularExpressions;
using System.Collections.Generic;
using AriyAI.ERP.Api.Models;
using ExcelDataReader;

namespace AriyAI.ERP.Api.Services
{
    public class ExcelParsingService
    {
        public List<ExtractedProductDto> ParseExcelProducts(string filePath)
        {
            var products = new List<ExtractedProductDto>();
            if (!File.Exists(filePath)) return products;

            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                using (var stream = File.Open(filePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = ExcelReaderFactory.CreateReader(stream))
                    {
                        var result = reader.AsDataSet(new ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = false // We scan rows ourselves to find headers dynamically
                            }
                        });

                        if (result.Tables.Count > 0)
                        {
                            var table = result.Tables[0];
                            int headerRowIdx = -1;
                            
                            int qtyCol = -1;
                            int descCol = -1;
                            int partCol = -1;
                            int makeCol = -1;
                            int modelCol = -1;

                            var qtyRegex = new Regex(@"\b(qty|quantity|pcs|nos|pieces|qly|qtyi|pos|q\.)\b", RegexOptions.IgnoreCase);
                            var descRegex = new Regex(@"\b(description|product|name|details|item|particulars)\b", RegexOptions.IgnoreCase);
                            var partRegex = new Regex(@"\b(part\s*code|part\s*number|part\s*no|code|catalog)\b", RegexOptions.IgnoreCase);
                            var makeRegex = new Regex(@"\b(make|brand|manufacturer)\b", RegexOptions.IgnoreCase);
                            var modelRegex = new Regex(@"\b(model)\b", RegexOptions.IgnoreCase);

                            // 1. Scan the first 15 rows for headers
                            int scanRows = Math.Min(15, table.Rows.Count);
                            for (int r = 0; r < scanRows; r++)
                            {
                                var row = table.Rows[r];
                                int matchedHeaders = 0;

                                int tempQty = -1;
                                int tempDesc = -1;
                                int tempPart = -1;
                                int tempMake = -1;
                                int tempModel = -1;

                                for (int c = 0; c < table.Columns.Count; c++)
                                {
                                    string cellVal = row[c]?.ToString() ?? "";
                                    if (string.IsNullOrWhiteSpace(cellVal)) continue;

                                    if (qtyRegex.IsMatch(cellVal)) { tempQty = c; matchedHeaders++; }
                                    else if (partRegex.IsMatch(cellVal)) { tempPart = c; matchedHeaders++; }
                                    else if (descRegex.IsMatch(cellVal)) { tempDesc = c; matchedHeaders++; }
                                    else if (makeRegex.IsMatch(cellVal)) { tempMake = c; matchedHeaders++; }
                                    else if (modelRegex.IsMatch(cellVal)) { tempModel = c; matchedHeaders++; }
                                }

                                // If we matched at least 2 key headers, this is the header row
                                if (matchedHeaders >= 2 && (tempDesc != -1 || tempPart != -1))
                                {
                                    headerRowIdx = r;
                                    qtyCol = tempQty;
                                    descCol = tempDesc;
                                    partCol = tempPart;
                                    makeCol = tempMake;
                                    modelCol = tempModel;
                                    break;
                                }
                            }

                            // 2. If headers detected, parse rows beneath it
                            if (headerRowIdx != -1)
                            {
                                for (int r = headerRowIdx + 1; r < table.Rows.Count; r++)
                                {
                                    var row = table.Rows[r];
                                    
                                    // Check if row is empty
                                    if (row.ItemArray.All(x => x == null || x == DBNull.Value || string.IsNullOrWhiteSpace(x.ToString())))
                                        continue;

                                    string description = descCol != -1 ? row[descCol]?.ToString()?.Trim() ?? "" : "";
                                    string partNumber = partCol != -1 ? row[partCol]?.ToString()?.Trim() ?? "" : "";
                                    string make = makeCol != -1 ? row[makeCol]?.ToString()?.Trim() ?? "" : "";
                                    string model = modelCol != -1 ? row[modelCol]?.ToString()?.Trim() ?? "" : "";
                                    int quantity = 1;

                                    if (qtyCol != -1)
                                    {
                                        var qtyStr = row[qtyCol]?.ToString() ?? "";
                                        if (double.TryParse(Regex.Replace(qtyStr, @"[^\d\.]", ""), out double dVal))
                                        {
                                            quantity = (int)Math.Round(dVal);
                                        }
                                    }

                                    // Fallback if desc is empty but partNo exists
                                    if (string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(partNumber))
                                    {
                                        description = partNumber;
                                    }

                                    if (!string.IsNullOrEmpty(description))
                                    {
                                        products.Add(new ExtractedProductDto
                                        {
                                            Group = make,
                                            ProductDescription = description,
                                            PartNumber = partNumber,
                                            Make = make,
                                            Model = string.IsNullOrEmpty(model) ? partNumber : model,
                                            Quantity = quantity,
                                            Mapping = "Matched"
                                        });
                                    }
                                }
                            }
                            else
                            {
                                // Fallback Regex parsing for spreadsheets without headers
                                for (int r = 0; r < table.Rows.Count; r++)
                                {
                                    var row = table.Rows[r];
                                    var cells = row.ItemArray
                                        .Select(c => c?.ToString()?.Trim() ?? "")
                                        .Where(c => !string.IsNullOrEmpty(c))
                                        .ToList();

                                    if (cells.Count == 0) continue;

                                    string lineText = string.Join(" | ", cells);
                                    
                                    var parsedLine = ParseLineFallback(lineText);
                                    if (parsedLine != null)
                                    {
                                        products.Add(parsedLine);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Excel parse error: {ex.Message}");
            }

            return products;
        }

        private ExtractedProductDto? ParseLineFallback(string line)
        {
            if (line.Length < 3) return null;

            int quantity = 1;
            var qtyMatch = Regex.Match(line, @"\b(\d+)\s*(?:pcs|nos|qty|pieces|pieces)\b", RegexOptions.IgnoreCase);
            if (qtyMatch.Success)
            {
                int.TryParse(qtyMatch.Groups[1].Value, out quantity);
                line = line.Replace(qtyMatch.Value, " ");
            }

            string[] parts = line.Split(new[] { '|' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .ToArray();

            if (parts.Length == 0) return null;

            string desc = parts[0];
            string partNo = parts.Length > 1 ? parts[1] : "";
            string make = parts.Length > 2 ? parts[2] : "";

            if (string.IsNullOrEmpty(desc)) return null;

            return new ExtractedProductDto
            {
                ProductDescription = desc,
                PartNumber = partNo,
                Make = make,
                Model = partNo,
                Quantity = quantity,
                Mapping = "Matched"
            };
        }
    }
}
