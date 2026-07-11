using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using AriyAI.ERP.Api.Models;
using AriyAI.ERP.Api.Data;

namespace AriyAI.ERP.Api.Services
{
    public class ExtractedProductDto
    {
        public string Group { get; set; } = string.Empty;
        public string ProductDescription { get; set; } = string.Empty;
        public string PartNumber { get; set; } = string.Empty;
        public string Make { get; set; } = string.Empty;
        public string Model { get; set; } = string.Empty;
        public int Quantity { get; set; } = 1;
        public decimal Rate { get; set; } = 0;
        public string Mapping { get; set; } = "Matched";
    }

    public class ExtractionService
    {
        private readonly ErpDbContext _db;

        public ExtractionService(ErpDbContext db)
        {
            _db = db;
        }

        public List<ExtractedProductDto> ExtractProducts(string ocrText)
        {
            var results = new List<ExtractedProductDto>();
            if (string.IsNullOrWhiteSpace(ocrText)) return results;

            var lines = ocrText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            try
            {
                // Detect if the text contains a structured table
                bool isTable = false;
                int headerLineIndex = -1;

                var brandRegex = new Regex(@"\b(?:brand|make|manufacturer)\b", RegexOptions.IgnoreCase);
                var partRegex = new Regex(@"\b(?:part\s*code|part\s*num(?:ber)?|part\s*no|model)\b", RegexOptions.IgnoreCase);
                var descRegex = new Regex(@"\b(?:description|product|name|details|item)\b", RegexOptions.IgnoreCase);
                var qtyRegex = new Regex(@"\b(?:qty|quantity|q)\b", RegexOptions.IgnoreCase);
                var unitRegex = new Regex(@"\b(?:unit|uom|nos|pcs)\b", RegexOptions.IgnoreCase);

                for (int i = 0; i < lines.Length; i++)
                {
                    string l = lines[i];
                    int matchedHeaders = 0;
                    if (brandRegex.IsMatch(l)) matchedHeaders++;
                    if (partRegex.IsMatch(l)) matchedHeaders++;
                    if (descRegex.IsMatch(l)) matchedHeaders++;
                    if (qtyRegex.IsMatch(l)) matchedHeaders++;
                    if (unitRegex.IsMatch(l)) matchedHeaders++;

                    if (matchedHeaders >= 3)
                    {
                        isTable = true;
                        headerLineIndex = i;
                        break;
                    }
                }

                if (isTable)
                {
                    string headerLine = lines[headerLineIndex];
                    var headerParts = Regex.Split(headerLine, @"\s{2,}|\t")
                        .Select(h => h.Trim())
                        .Where(h => !string.IsNullOrEmpty(h))
                        .ToList();

                    if (headerParts.Count < 3)
                    {
                        string stdHeader = Regex.Replace(headerLine, @"\bpart\s+code\b", "PartCode", RegexOptions.IgnoreCase);
                        stdHeader = Regex.Replace(stdHeader, @"\bpart\s+number\b", "PartNumber", RegexOptions.IgnoreCase);
                        stdHeader = Regex.Replace(stdHeader, @"\bpart\s+no\b", "PartNo", RegexOptions.IgnoreCase);
                        stdHeader = Regex.Replace(stdHeader, @"\bsl\s+no\b", "SlNo", RegexOptions.IgnoreCase);

                        headerParts = stdHeader.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries)
                            .Select(h => h.Trim())
                            .ToList();
                    }

                    int brandColIdx = -1;
                    int partColIdx = -1;
                    int descColIdx = -1;
                    int qtyColIdx = -1;
                    int unitColIdx = -1;

                    for (int col = 0; col < headerParts.Count; col++)
                    {
                        string h = headerParts[col];
                        if (brandRegex.IsMatch(h)) brandColIdx = col;
                        else if (partRegex.IsMatch(h) || h.Equals("PartCode", StringComparison.OrdinalIgnoreCase) || h.Equals("PartNumber", StringComparison.OrdinalIgnoreCase) || h.Equals("PartNo", StringComparison.OrdinalIgnoreCase)) partColIdx = col;
                        else if (descRegex.IsMatch(h)) descColIdx = col;
                        else if (qtyRegex.IsMatch(h)) qtyColIdx = col;
                        else if (unitRegex.IsMatch(h)) unitColIdx = col;
                    }

                    for (int i = headerLineIndex + 1; i < lines.Length; i++)
                    {
                        string rowLine = lines[i].Trim();
                        if (string.IsNullOrEmpty(rowLine)) continue;

                        string rowLower = rowLine.ToLower();
                        if (rowLower.StartsWith("best regards") || rowLower.StartsWith("thanks") || rowLower.StartsWith("sincerely") || rowLower.StartsWith("regards"))
                        {
                            break;
                        }

                        var cells = Regex.Split(rowLine, @"\s{2,}|\t")
                            .Select(c => c.Trim())
                            .Where(c => !string.IsNullOrEmpty(c))
                            .ToList();

                        string make = "";
                        string partCode = "";
                        string description = "";
                        int qty = 1;

                        if (cells.Count >= 3 && cells.Count == headerParts.Count)
                        {
                            if (brandColIdx != -1 && brandColIdx < cells.Count) make = cells[brandColIdx];
                            if (partColIdx != -1 && partColIdx < cells.Count) partCode = cells[partColIdx];
                            if (descColIdx != -1 && descColIdx < cells.Count) description = cells[descColIdx];
                            if (qtyColIdx != -1 && qtyColIdx < cells.Count)
                            {
                                if (double.TryParse(Regex.Replace(cells[qtyColIdx], @"[^\d\.]", ""), out double dVal))
                                    qty = (int)Math.Round(dVal);
                            }
                        }
                        else
                        {
                            var rightMatch = Regex.Match(rowLine, @"\b(\d+(?:\.\d+)?)\s*([a-zA-Z]+)?\s*$");
                            if (rightMatch.Success)
                            {
                                if (double.TryParse(rightMatch.Groups[1].Value, out double dVal))
                                    qty = (int)Math.Round(dVal);

                                rowLine = rowLine.Substring(0, rightMatch.Index).Trim();
                            }

                            var words = rowLine.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries).ToList();
                            if (words.Count > 0)
                            {
                                int currentWordIdx = 0;
                                if (brandColIdx != -1 && currentWordIdx < words.Count)
                                {
                                    make = words[currentWordIdx];
                                    currentWordIdx++;
                                }
                                if (partColIdx != -1 && currentWordIdx < words.Count)
                                {
                                    partCode = words[currentWordIdx];
                                    currentWordIdx++;
                                }
                                if (currentWordIdx < words.Count)
                                {
                                    description = string.Join(" ", words.Skip(currentWordIdx));
                                }
                            }
                        }

                        if (string.IsNullOrEmpty(description) && !string.IsNullOrEmpty(partCode))
                        {
                            description = partCode;
                        }

                        if (!string.IsNullOrEmpty(description))
                        {
                            results.Add(new ExtractedProductDto
                            {
                                ProductDescription = description,
                                PartNumber = partCode,
                                Make = make,
                                Quantity = qty,
                                Mapping = "Matched"
                            });
                        }
                    }

                    if (results.Count > 0)
                    {
                        return results;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Table parsing fallback: {ex.Message}");
            }

            // Fallback: Existing line-by-line parsing logic
            // Load all active catalog codes from database
            var catalogCodes = _db.Products.Select(p => p.PartNumber).Where(c => !string.IsNullOrEmpty(c)).ToList();
            var sortedCodes = catalogCodes.OrderByDescending(c => c.Length).ToList();

            foreach (var rawLine in lines)
            {
                var line = rawLine.Trim();
                if (line.Length < 3) continue;

                // Clean leading list indicators / serial numbers
                line = Regex.Replace(line, @"^\s*\(?\d+\)?[\s\.\-\/]*", "", RegexOptions.IgnoreCase).Trim();
                line = Regex.Replace(line, @"^\s*sl\s*no\b[\s\.\-\/]*", "", RegexOptions.IgnoreCase).Trim();

                if (line.Length < 3) continue;

                string? productCode = null;
                int? quantity = null;

                // 1. Match catalog code first
                string lineNorm = NormalizeCode(line);
                foreach (var code in sortedCodes)
                {
                    string codeNorm = NormalizeCode(code);
                    if (codeNorm.Length >= 3 && lineNorm.Contains(codeNorm))
                    {
                        productCode = code;
                        break;
                    }
                }

                string remainder = line;
                if (productCode != null)
                {
                    // Strip code from line carefully
                    var patternChars = productCode.Where(char.IsLetterOrDigit);
                    var patternRegex = string.Join(@"\s*", patternChars.Select(c => Regex.Escape(c.ToString())));
                    try
                    {
                        var match = Regex.Match(line, patternRegex, RegexOptions.IgnoreCase);
                        if (match.Success)
                        {
                            remainder = line.Substring(0, match.Index) + " " + line.Substring(match.Index + match.Length);
                        }
                    }
                    catch
                    {
                        remainder = line.Replace(productCode, " ");
                    }
                }

                // 2. Preprocess remainder spaces
                remainder = PreprocessText(remainder);

                // 3. Extract quantity
                var qtyPatterns = new[]
                {
                    new Regex(@"\b(?:qty|quantity|q|nos?|pcs|pieces|qly|qtyi|pos)\s*[:\-=\s\.]*\s*(\d{1,4}(?:\.\d+)?)\b", RegexOptions.IgnoreCase),
                    new Regex(@"\b(\d{1,4}(?:\.\d+)?)\s*(?:qty|quantity|q|nos?|pcs|pieces|qly|qtyi|pos)\b", RegexOptions.IgnoreCase),
                    new Regex(@"\b(?:x|×)\s*(\d{1,4}(?:\.\d+)?)\b", RegexOptions.IgnoreCase),
                    new Regex(@"\b(\d{1,4}(?:\.\d+)?)\s*(?:x|×)\b", RegexOptions.IgnoreCase)
                };

                bool qtyMatched = false;
                foreach (var pat in qtyPatterns)
                {
                    var match = pat.Match(remainder);
                    if (match.Success)
                    {
                        if (double.TryParse(match.Groups[1].Value, out double dVal))
                        {
                            quantity = (int)Math.Round(dVal);
                            qtyMatched = true;
                            remainder = pat.Replace(remainder, "").Trim();
                            break;
                        }
                    }
                }

                if (!qtyMatched)
                {
                    var trailingMatch = Regex.Match(remainder, @"[-–—\s](\d{1,4}(?:\.\d+)?)\s*$");
                    if (trailingMatch.Success)
                    {
                        var valStr = trailingMatch.Groups[1].Value;
                        if (productCode == null || !productCode.EndsWith(valStr))
                        {
                            if (double.TryParse(valStr, out double dVal))
                            {
                                quantity = (int)Math.Round(dVal);
                                remainder = remainder.Substring(0, trailingMatch.Index).Trim();
                                qtyMatched = true;
                            }
                        }
                    }
                }

                // Clean remainder representation
                string productName = remainder;
                productName = Regex.Replace(productName, @"^[\s\-\:\,\.\/]+|[\s\-\:\,\.\/]+$", "").Trim();

                if (!string.IsNullOrEmpty(productCode) && !string.IsNullOrEmpty(productName))
                {
                    if (!NormalizeCode(productName).Contains(NormalizeCode(productCode)))
                    {
                        productName = $"{productCode} {productName}".Trim();
                    }
                }
                else if (!string.IsNullOrEmpty(productCode))
                {
                    productName = productCode;
                }

                results.Add(new ExtractedProductDto
                {
                    ProductDescription = productName,
                    PartNumber = productCode ?? string.Empty,
                    Quantity = quantity ?? 1,
                    Mapping = "Matched"
                });
            }

            return results;
        }

        private string NormalizeCode(string code)
        {
            if (string.IsNullOrEmpty(code)) return "";
            string normalized = code.Trim().ToUpper();
            normalized = Regex.Replace(normalized, @"[^A-Z0-9]", "");
            bool hasDigit = normalized.Any(char.IsDigit);
            if (hasDigit)
            {
                var replacements = new Dictionary<char, char>
                {
                    { 'O', '0' }, { 'I', '1' }, { 'L', '1' }, { 'S', '5' }, { 'Z', '2' }, { 'B', '8' }, { 'G', '6' }
                };
                var chars = normalized.ToCharArray();
                for (int i = 0; i < chars.Length; i++)
                {
                    if (replacements.TryGetValue(chars[i], out char rep))
                    {
                        chars[i] = rep;
                    }
                }
                normalized = new string(chars);
            }
            return normalized;
        }

        private string PreprocessText(string text)
        {
            // Insert spaces between numbers and quantity indicators
            text = Regex.Replace(text, @"(\d+(?:\.\d+)?)\s*[-.:\s]*\s*(nos|qty|pcs|pieces|no|q|qly|qtyi|pos)\b", " $1 $2", RegexOptions.IgnoreCase);
            text = Regex.Replace(text, @"\b(qty|quantity|nos|pcs|pieces|q|qly|qtyi|pos)[-.:\s]*(\d+(?:\.\d+)?)\b", "$1 $2", RegexOptions.IgnoreCase);
            return text.Trim();
        }
    }
}
