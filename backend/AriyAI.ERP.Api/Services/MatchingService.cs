using System;
using System.Collections.Generic;
using System.Linq;
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
                int bestScore = 0;

                // 1. First search by exact part number matching
                if (!string.IsNullOrEmpty(item.PartNumber))
                {
                    var exactCodeMatch = dbProducts.FirstOrDefault(p => 
                        string.Equals(p.PartNumber?.Trim(), item.PartNumber.Trim(), StringComparison.OrdinalIgnoreCase));
                    
                    if (exactCodeMatch != null)
                    {
                        bestMatch = exactCodeMatch;
                        bestScore = 100;
                    }
                }

                // 2. If no exact code match, search using FuzzySharp against the description / name
                if (bestMatch == null)
                {
                    string target = item.ProductDescription.ToLower();
                    foreach (var p in dbProducts)
                    {
                        string choice = $"{p.Make} {p.PartNumber} {p.Description}".ToLower();
                        
                        // Calculate fuzzy ratios
                        int ratio = Fuzz.Ratio(target, choice);
                        int tokenSortRatio = Fuzz.TokenSortRatio(target, choice);
                        int maxScore = Math.Max(ratio, tokenSortRatio);

                        if (maxScore > bestScore)
                        {
                            bestScore = maxScore;
                            bestMatch = p;
                        }
                    }
                }

                // 3. Assign properties based on threshold (60%)
                if (bestMatch != null && bestScore >= 60)
                {
                    item.Group = bestMatch.Group;
                    item.ProductDescription = bestMatch.Description;
                    item.PartNumber = bestMatch.PartNumber;
                    item.Make = bestMatch.Make;
                    item.Model = bestMatch.Model;
                    item.Rate = bestMatch.Rate;
                    item.Mapping = "Matched";
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
    }
}
