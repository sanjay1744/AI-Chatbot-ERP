using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using AriyAI.ERP.Api.Data;

namespace AriyAI.ERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly ErpDbContext _context;
        private readonly IConfiguration _configuration;
        private static readonly HttpClient _httpClient = new HttpClient();

        public ChatController(ErpDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public class ChatRequest
        {
            public string Message { get; set; } = string.Empty;
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message cannot be empty.");
            }

            try
            {
                // Step 1: Text-to-SQL
                string sqlQuery = await GenerateSqlFromMessageAsync(request.Message);

                if (string.IsNullOrWhiteSpace(sqlQuery))
                {
                    return Ok(new
                    {
                        reply = "I couldn't translate that question into a database query. Could you try rephrasing it? E.g., 'What is the total sales for agent Ajith?'",
                        sql = "",
                        data = new List<object>()
                    });
                }

                // Clean SQL query formatting (sometimes LLMs wrap SQL inside code blocks even when asked not to)
                sqlQuery = CleanSqlQuery(sqlQuery);

                // SQL Safety Checks
                string upperQuery = sqlQuery.ToUpperInvariant();
                if (upperQuery.Contains("INSERT") || upperQuery.Contains("UPDATE") || upperQuery.Contains("DELETE") || 
                    upperQuery.Contains("DROP") || upperQuery.Contains("ALTER") || upperQuery.Contains("CREATE") || 
                    upperQuery.Contains("TRUNCATE"))
                {
                    return BadRequest(new
                    {
                        reply = "Safety block: Only read-only queries (SELECT) are permitted.",
                        sql = sqlQuery,
                        data = new List<object>()
                    });
                }

                // Step 2: Execute SQLite query
                var queryResults = new List<Dictionary<string, object>>();
                using (var connection = _context.Database.GetDbConnection())
                {
                    if (connection.State != ConnectionState.Open)
                    {
                        await connection.OpenAsync();
                    }

                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = sqlQuery;
                        using (var reader = await command.ExecuteReaderAsync())
                        {
                            while (await reader.ReadAsync())
                            {
                                var row = new Dictionary<string, object>();
                                for (int i = 0; i < reader.FieldCount; i++)
                                {
                                    row[reader.GetName(i)] = reader.GetValue(i);
                                }
                                queryResults.Add(row);
                            }
                        }
                    }
                }

                // Step 3: Synthesis of final natural language response
                string resultsJson = JsonSerializer.Serialize(queryResults);
                string reply = await SynthesizeAnswerAsync(request.Message, sqlQuery, resultsJson);

                return Ok(new
                {
                    reply = reply,
                    sql = sqlQuery,
                    data = queryResults
                });
            }
            catch (HttpRequestException httpEx)
            {
                string friendlyMessage = $"I couldn't connect to your local Ollama instance ({httpEx.Message}). \n\nPlease ensure that:\n1. **Ollama** is running on your machine.\n2. You have pulled the model using: `ollama pull qwen2.5-coder:1.5b`";
                return Ok(new
                {
                    reply = friendlyMessage,
                    sql = "",
                    data = new List<object>()
                });
            }
            catch (Exception ex)
            {
                return Ok(new
                {
                    reply = $"An error occurred while executing the query. Error details: {ex.Message}",
                    sql = "",
                    data = new List<object>()
                });
            }
        }

        private async Task<string> GenerateSqlFromMessageAsync(string message)
        {
            var systemPrompt = @"You are a SQLite expert database translator.
Generate a read-only SQLite SELECT query to answer the user's question.
IMPORTANT: Return ONLY the raw SQL query. Do not wrap in ```sql, do not explain, do not add HTML/markdown formatting. Return only the SQL text.

DATABASE SCHEMA:
1. `SalesRecords` (contains live invoice lines imported from sales.xlsx)
   - Id: INTEGER (Primary Key)
   - InvoiceDate: TEXT (formatted as YYYY-MM-DD)
   - InvoiceNumber: TEXT
   - CustomerId: INTEGER
   - CustomerName: TEXT
   - ItemId: INTEGER
   - ItemName: TEXT
   - Uom: TEXT
   - Qty: INTEGER
   - Rate: NUMERIC
   - AgentId: INTEGER
   - AgentName: TEXT

2. `Customers` (CRM master customer table)
   - Id: INTEGER
   - Name: TEXT
   - Address: TEXT
   - City: TEXT
   - State: TEXT
   - Country: TEXT

3. `Agents` (CRM sales agents table)
   - Id: INTEGER
   - Name: TEXT
   - Email: TEXT
   - Phone: TEXT

4. `Products` (Inventory product catalogs)
   - Id: INTEGER
   - [Group]: TEXT
   - Description: TEXT
   - PartNumber: TEXT
   - Make: TEXT
   - Model: TEXT
   - Rate: NUMERIC

5. `SalesEnquiries` (CRM enquiries)
   - Id: INTEGER
   - EnquiryNumber: TEXT
   - EnquiryDate: TEXT
   - CustomerId: INTEGER
   - AgentId: INTEGER
   - Status: TEXT
   - Remarks: TEXT

6. `Quotations` (CRM quotations)
   - Id: INTEGER
   - QuotationNumber: TEXT
   - QuotationDate: TEXT
   - CustomerId: INTEGER
   - AgentId: INTEGER
   - Status: TEXT
   - Subject1: TEXT

RULES & TIPS:
- Calculate Sales revenue as: Qty * Rate
- Use LIKE with wildcards for text comparison to avoid spelling or casing mismatch (e.g. AgentName LIKE '%Thalaimalai%' or CustomerName LIKE '%Premier%').
- For date ranges, compare InvoiceDate string like: InvoiceDate >= '2025-01-01'
- Wrap table columns named after SQLite reserved keywords in square brackets, e.g. [Group] in Products.
- Keep columns simple. Use aliases where appropriate for readability.
- Limit output rows if not specified but likely to be massive (e.g., LIMIT 50).

User Question: ";

            var fullText = systemPrompt + message;
            return await CallOllamaApiAsync(fullText);
        }

        private async Task<string> SynthesizeAnswerAsync(string message, string sql, string dataJson)
        {
            var prompt = $@"You are AriyAI, an intelligent ERP & CRM Chatbot Assistant.
The user asked: ""{message}""
We translated it to this SQLite query:
{sql}

The execution of this query returned these results from the database:
{dataJson}

Please compose a natural, professional response explaining the results clearly.
Format the response beautifully in Markdown.
- If there are numbers or money, format them nicely (e.g., currency as INR, thousand separators).
- If the results contain multiple records, format them in a Markdown table.
- Be concise and clear. Do not mention that a SQL query was executed unless asked; just present the findings.
- If no results are returned, inform the user politely.";

            return await CallOllamaApiAsync(prompt);
        }

        private async Task<string> CallOllamaApiAsync(string prompt)
        {
            string baseUrl = _configuration["Ollama:BaseUrl"] ?? "http://localhost:11434";
            string model = Environment.GetEnvironmentVariable("OLLAMA_MODEL");
            if (string.IsNullOrEmpty(model))
            {
                model = _configuration["Ollama:Model"] ?? "qwen2.5-coder:1.5b";
            }

            var requestBody = new OllamaRequest
            {
                model = model,
                prompt = prompt,
                stream = false
            };

            string url = $"{baseUrl.TrimEnd('/')}/api/generate";
            var json = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent);
            response.EnsureSuccessStatusCode();

            var responseJson = await response.Content.ReadAsStringAsync();
            var ollamaResponse = JsonSerializer.Deserialize<OllamaResponse>(responseJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            
            return ollamaResponse?.response?.Trim() ?? string.Empty;
        }

        private string CleanSqlQuery(string sql)
        {
            // Remove markdown code blocks if any
            if (sql.StartsWith("```sql", StringComparison.OrdinalIgnoreCase))
            {
                sql = sql.Substring(6);
            }
            else if (sql.StartsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                sql = sql.Substring(3);
            }

            if (sql.EndsWith("```", StringComparison.OrdinalIgnoreCase))
            {
                sql = sql.Substring(0, sql.Length - 3);
            }

            return sql.Trim();
        }

        public class OllamaRequest
        {
            public string model { get; set; } = string.Empty;
            public string prompt { get; set; } = string.Empty;
            public bool stream { get; set; } = false;
        }

        public class OllamaResponse
        {
            public string model { get; set; } = string.Empty;
            public string response { get; set; } = string.Empty;
            public bool done { get; set; }
        }
    }
}
