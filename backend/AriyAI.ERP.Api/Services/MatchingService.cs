using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using FuzzySharp;
using AriyAI.ERP.Api.Models;
using AriyAI.ERP.Api.Data;

namespace AriyAI.ERP.Api.Services
{
    public class MatchingService
    {
        private readonly ErpDbContext _db;

        public MatchingService(ErpDbContext db)
        {
            _db = db;
        }

        public List<ExtractedProductDto> MatchProducts(List<ExtractedProductDto> extractedItems)
        {
            var dbProducts = _db.Products.ToList();
            
            // Build unique set of known brands for context matching
            var knownBrands = dbProducts
                .Select(p => p.Make?.ToUpper())
                .Where(b => !string.IsNullOrEmpty(b))
                .Distinct()
                .ToHashSet();

            foreach (var item in extractedItems)
            {
                if (dbProducts.Count == 0)
                {
                    item.Mapping = "Unmapped";
                    if (string.IsNullOrEmpty(item.Make) || item.Make == "—")
                    {
                        item.Make = "—";
                    }
                    item.Model = item.PartNumber;
                    item.Rate = 0;
                    continue;
                }

                Product? bestMatch = null;
                double bestScore = 0;
                string matchMethod = "none";

                string targetNameClean = CleanExtractedText(item.ProductDescription);
                string targetCodeClean = CleanExtractedText(item.PartNumber);

                // Brand weight context detection
                string? extractedBrand = null;
                foreach (var brand in knownBrands)
                {
                    if (Regex.IsMatch(targetNameClean, $@"\b{Regex.Escape(brand)}\b", RegexOptions.IgnoreCase) ||
                        Regex.IsMatch(targetCodeClean, $@"\b{Regex.Escape(brand)}\b", RegexOptions.IgnoreCase))
                    {
                        extractedBrand = brand;
                        break;
                    }
                }

                // Strategy 1: Exact code match (100% confidence)
                if (!string.IsNullOrEmpty(item.PartNumber))
                {
                    var exactCodeMatch = dbProducts.FirstOrDefault(p => 
                        string.Equals(p.PartNumber?.Trim(), item.PartNumber.Trim(), StringComparison.OrdinalIgnoreCase));
                    
                    if (exactCodeMatch != null)
                    {
                        bestMatch = exactCodeMatch;
                        bestScore = 100;
                        matchMethod = "exact_code";
                    }
                }

                // Strategy 2: Normalized code match (98% confidence, handling OCR confusion)
                if (bestMatch == null && !string.IsNullOrEmpty(item.PartNumber))
                {
                    string normExtracted = NormalizeCode(item.PartNumber);
                    if (!string.IsNullOrEmpty(normExtracted))
                    {
                        var normCodeMatch = dbProducts.FirstOrDefault(p => 
                            NormalizeCode(p.PartNumber) == normExtracted);
                        
                        if (normCodeMatch != null)
                        {
                            bestMatch = normCodeMatch;
                            bestScore = 98;
                            matchMethod = "normalized_code";
                        }
                    }
                }

                // Strategy 3: Try extracting code from name and match normalized (95% confidence)
                if (bestMatch == null && !string.IsNullOrEmpty(targetNameClean))
                {
                    var tokens = Regex.Split(targetNameClean, @"[\s\-,\.\/]+");
                    foreach (var token in tokens)
                    {
                        string normToken = NormalizeCode(token);
                        if (normToken.Length >= 3)
                        {
                            var tokenMatch = dbProducts.FirstOrDefault(p => 
                                NormalizeCode(p.PartNumber) == normToken);
                            
                            if (tokenMatch != null)
                            {
                                bestMatch = tokenMatch;
                                bestScore = 95;
                                matchMethod = "normalized_code_from_name";
                                break;
                            }
                        }
                    }
                }

                // Strategy 4-6: Fuzzy matching strategies (weighted by brand if detected)
                if (bestMatch == null)
                {
                    Action<List<Product>> runFuzzySearch = (choices) =>
                    {
                        foreach (var p in choices)
                        {
                            double currentScore = 0;
                            string method = "none";

                            // Fuzzy code similarity (incorporating trigrams)
                            if (!string.IsNullOrEmpty(item.PartNumber) && !string.IsNullOrEmpty(p.PartNumber))
                            {
                                int ratio = Fuzz.Ratio(item.PartNumber.ToLower(), p.PartNumber.ToLower());
                                double trigramCode = TrigramSimilarity(item.PartNumber, p.PartNumber);
                                double combined = Math.Max(ratio, trigramCode) * 1.2;
                                double score = Math.Min(100.0, combined);

                                if (score > currentScore)
                                {
                                    currentScore = score;
                                    method = "fuzzy_code";
                                }
                            }

                            // Fuzzy name similarity (against catalog description directly, avoiding dilution)
                            if (!string.IsNullOrEmpty(targetNameClean) && !string.IsNullOrEmpty(p.Description))
                            {
                                int tokenSortRatio = Fuzz.TokenSortRatio(targetNameClean.ToLower(), p.Description.ToLower());
                                if (tokenSortRatio > currentScore)
                                {
                                    currentScore = tokenSortRatio;
                                    method = "fuzzy_name";
                                }
                            }

                            // Partial token set check
                            if (!string.IsNullOrEmpty(targetNameClean) && !string.IsNullOrEmpty(p.Description))
                            {
                                string combined = $"{p.PartNumber} {p.Description}".ToLower();
                                int tokenSet = Fuzz.TokenSetRatio(targetNameClean.ToLower(), combined);
                                if (tokenSet > currentScore)
                                {
                                    currentScore = tokenSet;
                                    method = "token_set";
                                }
                            }

                            // Boost the score if it matches the detected brand context
                            if (extractedBrand != null && string.Equals(p.Make, extractedBrand, StringComparison.OrdinalIgnoreCase))
                            {
                                currentScore = Math.Min(100.0, currentScore * 1.1);
                            }

                            if (currentScore > bestScore)
                            {
                                bestScore = currentScore;
                                bestMatch = p;
                                matchMethod = method;
                            }
                        }
                    };

                    // Run fuzzy match with brand filter first (if present)
                    if (extractedBrand != null)
                    {
                        var brandFiltered = dbProducts
                            .Where(p => string.Equals(p.Make, extractedBrand, StringComparison.OrdinalIgnoreCase))
                            .ToList();
                        
                        runFuzzySearch(brandFiltered);

                        // If no match found or match is weak, fallback to full catalog search
                        if (bestMatch == null || bestScore < 75)
                        {
                            runFuzzySearch(dbProducts);
                        }
                    }
                    else
                    {
                        runFuzzySearch(dbProducts);
                    }
                }

                // Assign properties based on threshold (75% confidence)
                if (bestMatch != null && bestScore >= 75)
                {
                    item.Group = bestMatch.Group;
                    item.ProductDescription = bestMatch.Description;
                    item.PartNumber = bestMatch.PartNumber;
                    item.Make = bestMatch.Make;
                    item.Model = bestMatch.Model;
                    item.Rate = bestMatch.Rate;
                    item.Mapping = "Mapped";
                }
                else
                {
                    item.Group = "Unmapped";
                    if (string.IsNullOrEmpty(item.Make) || item.Make == "—")
                    {
                        item.Make = "—";
                    }
                    item.Model = item.PartNumber;
                    item.Rate = 0;
                    item.Mapping = "Unmapped";
                }
            }

            return extractedItems;
        }

        private string CleanExtractedText(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            string cleaned = text.ToUpper().Trim();

            var typoMapping = new Dictionary<string, string>
            {
                { "PUC", "PVC" },
                { "SLEMENS", "SIEMENS" },
                { "SEIMENS", "SIEMENS" },
                { "SENIDER", "SCHNEIDER" },
                { "SNEIDER", "SCHNEIDER" },
                { "OMRM", "OMRON" },
                { "OMRN", "OMRON" },
                { "STOPPEN", "STOPPER" },
                { "STOPER", "STOPPER" },
                { "BREKER", "BREAKER" },
                { "CONTATOR", "CONTACTOR" },
                { "HOXTOOMM", "40X40MM" },
                { "HOXTOOM", "40X40MM" },
                { "157", "15T" }
            };

            foreach (var kvp in typoMapping)
            {
                cleaned = Regex.Replace(cleaned, $@"\b{Regex.Escape(kvp.Key)}\b", kvp.Value);
            }

            return cleaned;
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

        private double TrigramSimilarity(string s1, string s2)
        {
            if (string.IsNullOrEmpty(s1) || string.IsNullOrEmpty(s2)) return 0.0;

            var getTrigrams = new Func<string, HashSet<string>>(s =>
            {
                string clean = s.Trim().ToUpper();
                var set = new HashSet<string>();
                if (clean.Length < 3)
                {
                    for (int i = 0; i < clean.Length - 1; i++)
                    {
                        set.Add(clean.Substring(i, 2));
                    }
                    if (clean.Length == 1)
                    {
                        set.Add(clean);
                    }
                    return set;
                }
                for (int i = 0; i < clean.Length - 2; i++)
                {
                    set.Add(clean.Substring(i, 3));
                }
                return set;
            });

            var t1 = getTrigrams(s1);
            var t2 = getTrigrams(s2);

            int intersection = t1.Intersect(t2).Count();
            int union = t1.Union(t2).Count();

            if (union == 0) return 0.0;
            return ((double)intersection / union) * 100.0;
        }
    }
}
