using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using ExcelDataReader;
using AriyAI.ERP.Api.Data;
using AriyAI.ERP.Api.Models;

namespace AriyAI.ERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class ChatController : ControllerBase
    {
        private readonly IConfiguration _configuration;
        private readonly ErpDbContext _context;
        private static readonly HttpClient _httpClient = new HttpClient();

        // In-memory SQLite shared cache fields
        private static SqliteConnection? _inMemoryConnection;
        private static readonly object _lockObject = new object();
        private static bool _isExcelLoaded = false;
        private static DateTime _lastExcelWriteTime = DateTime.MinValue;

        public ChatController(IConfiguration configuration, ErpDbContext context)
        {
            _configuration = configuration;
            _context = context;
        }

        public class ChatMessageDto
        {
            public string Sender { get; set; } = string.Empty;
            public string Text { get; set; } = string.Empty;
        }

        public class ChatRequest
        {
            public int? SessionId { get; set; }
            public string Message { get; set; } = string.Empty;
        }

        // GET api/chat/sessions
        [HttpGet("sessions")]
        public async Task<IActionResult> GetSessions()
        {
            var sessions = await _context.ChatSessions
                .OrderByDescending(s => s.UpdatedAt)
                .Select(s => new {
                    s.Id,
                    s.Title,
                    s.CreatedAt,
                    s.UpdatedAt
                })
                .ToListAsync();

            return Ok(sessions);
        }

        // GET api/chat/sessions/{id}
        [HttpGet("sessions/{id}")]
        public async Task<IActionResult> GetSession(int id)
        {
            var session = await _context.ChatSessions
                .Include(s => s.Messages)
                .FirstOrDefaultAsync(s => s.Id == id);

            if (session == null)
            {
                return NotFound("Chat session not found.");
            }

            var messages = session.Messages
                .OrderBy(m => m.Timestamp)
                .Select(m => new {
                    m.Id,
                    m.Sender,
                    m.Text,
                    m.Sql,
                    Data = string.IsNullOrEmpty(m.Data) ? null : JsonSerializer.Deserialize<List<Dictionary<string, object>>>(m.Data),
                    Chart = string.IsNullOrEmpty(m.Chart) ? null : JsonSerializer.Deserialize<object>(m.Chart),
                    m.Timestamp
                })
                .ToList();

            return Ok(new {
                session.Id,
                session.Title,
                session.CreatedAt,
                session.UpdatedAt,
                Messages = messages
            });
        }

        // POST api/chat/sessions
        [HttpPost("sessions")]
        public async Task<IActionResult> CreateSession([FromBody] CreateSessionDto dto)
        {
            var session = new ChatSession
            {
                Title = string.IsNullOrWhiteSpace(dto?.Title) ? "New Chat" : dto.Title,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.ChatSessions.Add(session);
            await _context.SaveChangesAsync();

            return Ok(session);
        }

        public class CreateSessionDto
        {
            public string Title { get; set; } = string.Empty;
        }

        // PUT api/chat/sessions/{id}
        [HttpPut("sessions/{id}")]
        public async Task<IActionResult> RenameSession(int id, [FromBody] RenameSessionDto dto)
        {
            var session = await _context.ChatSessions.FindAsync(id);
            if (session == null)
            {
                return NotFound("Chat session not found.");
            }

            session.Title = dto.Title;
            session.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return Ok(session);
        }

        public class RenameSessionDto
        {
            public string Title { get; set; } = string.Empty;
        }

        // DELETE api/chat/sessions/{id}
        [HttpDelete("sessions/{id}")]
        public async Task<IActionResult> DeleteSession(int id)
        {
            var session = await _context.ChatSessions.FindAsync(id);
            if (session == null)
            {
                return NotFound("Chat session not found.");
            }

            _context.ChatSessions.Remove(session);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Chat session deleted successfully." });
        }

        private async Task<string> GenerateSessionTitleAsync(string userMessage, CancellationToken cancellationToken)
        {
            try
            {
                string prompt = $"Create a short 3-6 word title for a conversation that begins with the following user prompt. Respond ONLY with the title. Do not add quotes, explanation, or punctuation.\n\nUser Prompt: {userMessage}\n\nTitle:";
                string title = await CallOllamaApiAsync(prompt, temperature: 0.5, numPredict: 15, cancellationToken: cancellationToken);
                title = title.Trim().Trim('"', '\'');
                if (!string.IsNullOrWhiteSpace(title) && title.Length <= 50)
                {
                    return title;
                }
            }
            catch
            {
                // Fallback
            }

            if (userMessage.Length <= 30) return userMessage;
            return userMessage.Substring(0, 27) + "...";
        }

        [HttpPost]
        public async Task<IActionResult> Ask([FromBody] ChatRequest request, CancellationToken cancellationToken)
        {
            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest("Message cannot be empty.");
            }

            ChatSession? session = null;
            bool isNewSession = false;

            try
            {
                // Find or create the session
                if (request.SessionId == null || request.SessionId == 0)
                {
                    session = new ChatSession
                    {
                        Title = "New Chat",
                        CreatedAt = DateTime.UtcNow,
                        UpdatedAt = DateTime.UtcNow
                    };
                    _context.ChatSessions.Add(session);
                    await _context.SaveChangesAsync(cancellationToken);
                    isNewSession = true;
                }
                else
                {
                    session = await _context.ChatSessions
                        .Include(s => s.Messages)
                        .FirstOrDefaultAsync(s => s.Id == request.SessionId, cancellationToken);

                    if (session == null)
                    {
                        return NotFound("Chat session not found.");
                    }
                }

                if (isNewSession)
                {
                    session.Title = await GenerateSessionTitleAsync(request.Message, cancellationToken);
                }

                var history = session.Messages
                    .OrderBy(m => m.Timestamp)
                    .Select(m => new ChatMessageDto { Sender = m.Sender, Text = m.Text })
                    .ToList();

                // Step 1: Text-to-SQL or Direct Conversation via LLM
                string modelOutput = await GenerateSqlFromMessageAsync(request.Message, history, cancellationToken);

                if (string.IsNullOrWhiteSpace(modelOutput))
                {
                    string fallbackReply = "I'm sorry, I couldn't process that. Could you please try again?";
                    
                    session.Messages.Add(new ChatMessage { Sender = "user", Text = request.Message, Timestamp = DateTime.UtcNow });
                    session.Messages.Add(new ChatMessage { Sender = "ai", Text = fallbackReply, Timestamp = DateTime.UtcNow });
                    session.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);

                    return Ok(new
                    {
                        sessionId = session.Id,
                        sessionTitle = session.Title,
                        reply = fallbackReply,
                        sql = "",
                        data = new List<object>(),
                        chart = (object?)null
                    });
                }

                // Try to extract a SQL query from the model's output
                bool isSqlQuery = TryExtractSqlQuery(modelOutput, out string sqlQuery);

                if (!isSqlQuery)
                {
                    // It is a direct conversational reply from the AI!
                    string cleanedOutput = CleanSqlQuery(modelOutput);

                    session.Messages.Add(new ChatMessage { Sender = "user", Text = request.Message, Timestamp = DateTime.UtcNow });
                    session.Messages.Add(new ChatMessage { Sender = "ai", Text = cleanedOutput, Timestamp = DateTime.UtcNow });
                    session.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);

                    return Ok(new
                    {
                        sessionId = session.Id,
                        sessionTitle = session.Title,
                        reply = cleanedOutput,
                        sql = "",
                        data = new List<object>(),
                        chart = (object?)null
                    });
                }

                // It is a SQL query!

                // SQL Safety Checks
                string upperQuery = sqlQuery.ToUpperInvariant();
                if (upperQuery.Contains("INSERT") || upperQuery.Contains("UPDATE") || upperQuery.Contains("DELETE") || 
                    upperQuery.Contains("DROP") || upperQuery.Contains("ALTER") || upperQuery.Contains("CREATE") || 
                    upperQuery.Contains("TRUNCATE"))
                {
                    string safetyReply = "Safety block: Only read-only queries (SELECT) are permitted.";

                    session.Messages.Add(new ChatMessage { Sender = "user", Text = request.Message, Timestamp = DateTime.UtcNow });
                    session.Messages.Add(new ChatMessage { Sender = "ai", Text = safetyReply, Sql = sqlQuery, Timestamp = DateTime.UtcNow });
                    session.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);

                    return BadRequest(new
                    {
                        sessionId = session.Id,
                        sessionTitle = session.Title,
                        reply = safetyReply,
                        sql = sqlQuery,
                        data = new List<object>(),
                        chart = (object?)null
                    });
                }

                // Ensure the Excel spreadsheet is loaded into our isolated in-memory DB
                await EnsureExcelLoadedAsync();

                // Step 2: Execute query against the in-memory SQLite database
                var queryResults = new List<Dictionary<string, object>>();
                using (var connection = new SqliteConnection("Data Source=ExcelInMemory;Mode=Memory;Cache=Shared"))
                {
                    await connection.OpenAsync(cancellationToken);
                    using (var command = connection.CreateCommand())
                    {
                        command.CommandText = sqlQuery;
                        using (var reader = await command.ExecuteReaderAsync(cancellationToken))
                        {
                            while (await reader.ReadAsync(cancellationToken))
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

                // Step 3: Synthesis of final natural response
                string resultsJson = JsonSerializer.Serialize(queryResults);
                string reply = await SynthesizeAnswerAsync(request.Message, sqlQuery, resultsJson, history, cancellationToken);

                var chartConfig = DetectAndBuildChart(sqlQuery, queryResults, request.Message);

                session.Messages.Add(new ChatMessage { Sender = "user", Text = request.Message, Timestamp = DateTime.UtcNow });
                session.Messages.Add(new ChatMessage 
                { 
                    Sender = "ai", 
                    Text = reply, 
                    Sql = sqlQuery, 
                    Data = resultsJson, 
                    Chart = chartConfig == null ? null : JsonSerializer.Serialize(chartConfig),
                    Timestamp = DateTime.UtcNow 
                });
                session.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync(cancellationToken);

                return Ok(new
                {
                    sessionId = session.Id,
                    sessionTitle = session.Title,
                    reply = reply,
                    sql = sqlQuery,
                    data = queryResults,
                    chart = chartConfig
                });
            }
            catch (HttpRequestException httpEx)
            {
                string friendlyMessage = $"I couldn't connect to your local Ollama instance ({httpEx.Message}). \n\nPlease ensure that:\n1. **Ollama** is running on your machine.\n2. You have pulled the model using: `ollama pull qwen2.5-coder:1.5b`";
                
                if (session != null)
                {
                    session.Messages.Add(new ChatMessage { Sender = "user", Text = request.Message, Timestamp = DateTime.UtcNow });
                    session.Messages.Add(new ChatMessage { Sender = "ai", Text = friendlyMessage, Timestamp = DateTime.UtcNow });
                    session.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return Ok(new
                {
                    sessionId = session?.Id ?? 0,
                    sessionTitle = session?.Title ?? "",
                    reply = friendlyMessage,
                    sql = "",
                    data = new List<object>(),
                    chart = (object?)null
                });
            }
            catch (Exception ex)
            {
                string errorMessage = $"An error occurred while executing the query. Error details: {ex.Message}";

                if (session != null)
                {
                    session.Messages.Add(new ChatMessage { Sender = "user", Text = request.Message, Timestamp = DateTime.UtcNow });
                    session.Messages.Add(new ChatMessage { Sender = "ai", Text = errorMessage, Timestamp = DateTime.UtcNow });
                    session.UpdatedAt = DateTime.UtcNow;
                    await _context.SaveChangesAsync(cancellationToken);
                }

                return Ok(new
                {
                    sessionId = session?.Id ?? 0,
                    sessionTitle = session?.Title ?? "",
                    reply = errorMessage,
                    sql = "",
                    data = new List<object>(),
                    chart = (object?)null
                });
            }
        }

        private async Task EnsureExcelLoadedAsync()
        {
            string excelPath = GetExcelPath();
            if (string.IsNullOrEmpty(excelPath))
            {
                throw new FileNotFoundException("Could not locate sales.xlsx file inside workspace.");
            }

            var currentWriteTime = System.IO.File.GetLastWriteTime(excelPath);

            if (_isExcelLoaded && currentWriteTime == _lastExcelWriteTime)
            {
                return;
            }

            lock (_lockObject)
            {
                currentWriteTime = System.IO.File.GetLastWriteTime(excelPath);
                if (_isExcelLoaded && currentWriteTime == _lastExcelWriteTime)
                {
                    return;
                }

                try
                {
                    if (_inMemoryConnection != null)
                    {
                        _inMemoryConnection.Close();
                        _inMemoryConnection.Dispose();
                        _inMemoryConnection = null;
                    }

                    // Open and keep the connection open to hold the in-memory database alive in the shared cache
                    _inMemoryConnection = new SqliteConnection("Data Source=ExcelInMemory;Mode=Memory;Cache=Shared");
                    _inMemoryConnection.Open();

                    // Create table
                    using (var command = _inMemoryConnection.CreateCommand())
                    {
                        command.CommandText = @"
                            DROP TABLE IF EXISTS SalesRecords;
                            CREATE TABLE SalesRecords (
                                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                                InvoiceDate TEXT,
                                InvoiceNumber TEXT,
                                CustomerId INTEGER,
                                CustomerName TEXT,
                                ItemId INTEGER,
                                ItemName TEXT,
                                Uom TEXT,
                                Qty INTEGER,
                                Rate NUMERIC,
                                AgentId INTEGER,
                                AgentName TEXT
                            );
                        ";
                        command.ExecuteNonQuery();
                    }

                    // Register CodePages for ExcelDataReader
                    System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

                    using (var stream = System.IO.File.Open(excelPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                    {
                        using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
                        {
                            var result = reader.AsDataSet(new ExcelDataReader.ExcelDataSetConfiguration()
                            {
                                ConfigureDataTable = (_) => new ExcelDataReader.ExcelDataTableConfiguration()
                                {
                                    UseHeaderRow = true
                                }
                            });

                            if (result.Tables.Count > 0)
                            {
                                var table = result.Tables[0];
                                
                                using (var transaction = _inMemoryConnection.BeginTransaction())
                                {
                                    using (var insertCmd = _inMemoryConnection.CreateCommand())
                                    {
                                        insertCmd.CommandText = @"
                                            INSERT INTO SalesRecords (
                                                InvoiceDate, InvoiceNumber, CustomerId, CustomerName,
                                                ItemId, ItemName, Uom, Qty, Rate, AgentId, AgentName
                                            ) VALUES (
                                                $invoiceDate, $invoiceNumber, $customerId, $customerName,
                                                $itemId, $itemName, $uom, $qty, $rate, $agentId, $agentName
                                            );
                                        ";

                                        var pInvoiceDate = insertCmd.CreateParameter(); pInvoiceDate.ParameterName = "$invoiceDate"; insertCmd.Parameters.Add(pInvoiceDate);
                                        var pInvoiceNumber = insertCmd.CreateParameter(); pInvoiceNumber.ParameterName = "$invoiceNumber"; insertCmd.Parameters.Add(pInvoiceNumber);
                                        var pCustomerId = insertCmd.CreateParameter(); pCustomerId.ParameterName = "$customerId"; insertCmd.Parameters.Add(pCustomerId);
                                        var pCustomerName = insertCmd.CreateParameter(); pCustomerName.ParameterName = "$customerName"; insertCmd.Parameters.Add(pCustomerName);
                                        var pItemId = insertCmd.CreateParameter(); pItemId.ParameterName = "$itemId"; insertCmd.Parameters.Add(pItemId);
                                        var pItemName = insertCmd.CreateParameter(); pItemName.ParameterName = "$itemName"; insertCmd.Parameters.Add(pItemName);
                                        var pUom = insertCmd.CreateParameter(); pUom.ParameterName = "$uom"; insertCmd.Parameters.Add(pUom);
                                        var pQty = insertCmd.CreateParameter(); pQty.ParameterName = "$qty"; insertCmd.Parameters.Add(pQty);
                                        var pRate = insertCmd.CreateParameter(); pRate.ParameterName = "$rate"; insertCmd.Parameters.Add(pRate);
                                        var pAgentId = insertCmd.CreateParameter(); pAgentId.ParameterName = "$agentId"; insertCmd.Parameters.Add(pAgentId);
                                        var pAgentName = insertCmd.CreateParameter(); pAgentName.ParameterName = "$agentName"; insertCmd.Parameters.Add(pAgentName);

                                        foreach (DataRow row in table.Rows)
                                        {
                                            DateTime invoiceDate = DateTime.Now;
                                            if (row["INVOICEDATE"] != DBNull.Value && row["INVOICEDATE"] != null)
                                            {
                                                var val = row["INVOICEDATE"].ToString();
                                                if (double.TryParse(val, out double oaDate))
                                                {
                                                    invoiceDate = DateTime.FromOADate(oaDate);
                                                }
                                                else if (DateTime.TryParse(val, out DateTime parsedDate))
                                                {
                                                    invoiceDate = parsedDate;
                                                }
                                            }

                                            pInvoiceDate.Value = invoiceDate.ToString("yyyy-MM-dd");
                                            pInvoiceNumber.Value = row["INVOICENUMBNER"] != DBNull.Value ? (row["INVOICENUMBNER"]?.ToString() ?? "") : "";

                                            int customerId = 0;
                                            if (row["CUSTOMERID"] != DBNull.Value && row["CUSTOMERID"] != null)
                                            {
                                                int.TryParse(row["CUSTOMERID"].ToString(), out customerId);
                                            }
                                            pCustomerId.Value = customerId;
                                            pCustomerName.Value = row["CUSTOMERNAME"] != DBNull.Value ? (row["CUSTOMERNAME"]?.ToString() ?? "") : "";

                                            int itemId = 0;
                                            if (row["ITEMID"] != DBNull.Value && row["ITEMID"] != null)
                                            {
                                                int.TryParse(row["ITEMID"].ToString(), out itemId);
                                            }
                                            pItemId.Value = itemId;
                                            pItemName.Value = row["ITEMNAME"] != DBNull.Value ? (row["ITEMNAME"]?.ToString() ?? "") : "";
                                            pUom.Value = row["UOM"] != DBNull.Value ? (row["UOM"]?.ToString() ?? "") : "";

                                            int qty = 0;
                                            if (row["QTY"] != DBNull.Value && row["QTY"] != null)
                                            {
                                                int.TryParse(row["QTY"].ToString(), out qty);
                                            }
                                            pQty.Value = qty;

                                            decimal rate = 0;
                                            if (row["RATE"] != DBNull.Value && row["RATE"] != null)
                                            {
                                                decimal.TryParse(row["RATE"].ToString(), out rate);
                                            }
                                            pRate.Value = rate;

                                            int agentId = 0;
                                            if (row["AGENTID"] != DBNull.Value && row["AGENTID"] != null)
                                            {
                                                int.TryParse(row["AGENTID"].ToString(), out agentId);
                                            }
                                            pAgentId.Value = agentId;
                                            pAgentName.Value = row["AGENTNAME"] != DBNull.Value ? (row["AGENTNAME"]?.ToString() ?? "") : "";

                                            insertCmd.ExecuteNonQuery();
                                        }
                                    }
                                    transaction.Commit();
                                }
                            }
                        }
                    }

                    _lastExcelWriteTime = currentWriteTime;
                    _isExcelLoaded = true;
                }
                catch (Exception ex)
                {
                    _isExcelLoaded = false;
                    _inMemoryConnection?.Dispose();
                    _inMemoryConnection = null;
                    throw new Exception($"Failed to load Excel data into memory: {ex.Message}", ex);
                }
            }
        }

        private string GetExcelPath()
        {
            string[] pathsToTry = new[]
            {
                "d:\\AriyAI\\chatbot_\\AI_Data\\sales.xlsx",
                Path.Combine(Directory.GetCurrentDirectory(), "AI_Data", "sales.xlsx"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "AI_Data", "sales.xlsx"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "..", "AI_Data", "sales.xlsx"),
                Path.Combine(Directory.GetCurrentDirectory(), "sales.xlsx"),
                Path.Combine(Directory.GetCurrentDirectory(), "..", "sales.xlsx")
            };

            foreach (var p in pathsToTry)
            {
                if (System.IO.File.Exists(p))
                {
                    return p;
                }
            }
            return string.Empty;
        }

        private async Task<string> GenerateSqlFromMessageAsync(string message, List<ChatMessageDto> history, CancellationToken cancellationToken)
        {
            var historyBuilder = new StringBuilder();
            if (history != null && history.Count > 0)
            {
                historyBuilder.AppendLine("CONVERSATION HISTORY (for context):");
                // Include up to 5 messages to provide compact history context (speeds up context pre-fill)
                int start = Math.Max(0, history.Count - 5);
                for (int i = start; i < history.Count; i++)
                {
                    string speaker = history[i].Sender.Equals("user", StringComparison.OrdinalIgnoreCase) ? "User" : "AI";
                    string text = history[i].Text;
                    // Truncate to 1000 characters to keep context small and fast
                    if (text.Length > 1000) text = text.Substring(0, 997) + "...";
                    historyBuilder.AppendLine($"- {speaker}: {text}");
                }
                historyBuilder.AppendLine();
            }

            var systemPrompt = @"You are AriyAI, an intelligent Manufacturing Copilot.
Analyze the user's input and the CONVERSATION HISTORY together.

CRITICAL DEFAULT RULE: If there is any conversation history about data/sales/agents/customers/products, then assume ANY short or ambiguous user input is a FOLLOW-UP to that previous conversation — NOT a casual greeting. Only treat a message as casual if the conversation has NO prior data context at all.

1. If the user's input is a question asking for data, statistics, calculations, summaries, names, or lists from the database/spreadsheet records (e.g. total sales, top customers, agent performance, quantities, or specific names of associated agents/customers/products), you MUST generate a read-only SQLite SELECT query.
   - Return ONLY the raw SQL query. Do not wrap in markdown code blocks. Do not explain.
   - Example: SELECT SUM(Qty * Rate) FROM SalesRecords;

2. FOLLOW-UP QUERIES: Users often type short, abbreviated, or informal follow-ups. You MUST interpret them using the conversation history and generate the appropriate SQL query. NEVER dismiss these as incomplete or casual.
   - Common abbreviations: ""y"" = ""why"", ""hw"" = ""how"", ""wt"" = ""what"", ""abt"" = ""about"", ""n"" = ""and"", ""pls"" = ""please"", ""thx"" = ""thanks""
   - Short follow-ups like ""and y"", ""and why"", ""why?"", ""how?"", ""y?"", ""who?"", ""show more"", ""details"", ""compare them"", ""list them"", ""show their name"", ""what about them?"", ""who is it?"" are ALL contextual data queries. Resolve them using conversation history.
   - Example: If the AI previously said ""GM MARKETING is the best agent"" and the user asks ""and y"" or ""y"", interpret it as ""why is GM MARKETING the best agent?"" and generate a SQL query to show GM MARKETING's total revenue: SELECT AgentName, SUM(Qty * Rate) AS TotalRevenue FROM SalesRecords WHERE AgentName LIKE '%GM MARKETING%' GROUP BY AgentName;
   - Example: If the user previously asked ""how many agents are associated with priyadharshini"" and now asks ""who is it"", you must generate: SELECT DISTINCT AgentName FROM SalesRecords WHERE CustomerName LIKE '%Priyadharshini%';

3. ONLY if the user's input is a completely generic/casual greeting with NO prior data conversation (e.g. ""hi"", ""how are you"", ""who are you"", ""tell me a joke"", ""thank you""), respond directly in a warm, helpful, and friendly conversational manner.

DATABASE SCHEMA (only query this table):
1. `SalesRecords` (loaded from the spreadsheet)
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

SQL RULES & TIPS:
- IMPORTANT: You MUST query the `SalesRecords` table. Do NOT query `Agents`, `Customers`, `Products`, or any other tables because they DO NOT EXIST in the database. All columns (like AgentName, CustomerName, ItemName) must be queried directly from `SalesRecords`.
- To list all agent names, query: SELECT DISTINCT AgentName FROM SalesRecords;
- To list all customer names, query: SELECT DISTINCT CustomerName FROM SalesRecords;
- To list all product/item names, query: SELECT DISTINCT ItemName FROM SalesRecords;
- When comparing multiple agents, customers, or products, select their name column and use GROUP BY (e.g., SELECT AgentName, SUM(Qty * Rate) FROM SalesRecords WHERE AgentName IN ('AARU TECH', 'NBM') GROUP BY AgentName;). Do NOT calculate a single aggregated sum without GROUP BY.
- CRITICAL: Never write SELECT SUM(...) WHERE AgentName = 'A' OR AgentName = 'B' without GROUP BY, because that combines their sales together instead of comparing them. You must always SELECT the AgentName column and use GROUP BY AgentName.
- Calculate Sales revenue as: Qty * Rate
- Use LIKE with wildcards for text comparison to avoid spelling or casing mismatch (e.g. AgentName LIKE '%Thalaimalai%' or CustomerName LIKE '%Premier%').
- For date ranges, compare InvoiceDate string like: InvoiceDate >= '2025-01-01'
- Limit output rows if likely to be massive (e.g., LIMIT 50).
- AMBIGUOUS ROLES (Agent vs. Customer): Many names (such as 'Ayush Agencies', 'Vashishtha Enterprises') exist in the database as both AgentName and CustomerName. If the user asks for the revenue, sales, or details of a name that could be either a Customer or an Agent (or you're not sure which role they mean), you MUST write a SQL query that retrieves results for BOTH roles using UNION ALL.
  Example:
  SELECT 'Customer' AS Role, CustomerName AS Name, SUM(Qty * Rate) AS Revenue FROM SalesRecords WHERE CustomerName LIKE '%Ayush%' GROUP BY CustomerName
  UNION ALL
  SELECT 'Agent' AS Role, AgentName AS Name, SUM(Qty * Rate) AS Revenue FROM SalesRecords WHERE AgentName LIKE '%Ayush%' GROUP BY AgentName;
- COMPARE THEM: If asked to compare two entities (e.g. ""compare them"" or ""compare Ayush and Vashishtha""), be consistent with the previous conversation's context. If they exist in both roles, compare them across both roles or match the previous conversation's intent. Do not randomly switch columns (e.g., do not switch from CustomerName to AgentName). Use GROUP BY appropriately.";

            var fullText = $"{systemPrompt}\n\n{historyBuilder}User Query: {message}\nAnswer/SQL:";
            return await CallOllamaApiAsync(fullText, temperature: 0.0, numPredict: 200, cancellationToken: cancellationToken);
        }

        private async Task<string> SynthesizeAnswerAsync(string message, string sql, string dataJson, List<ChatMessageDto> history, CancellationToken cancellationToken)
        {
            var historyBuilder = new StringBuilder();
            if (history != null && history.Count > 0)
            {
                historyBuilder.AppendLine("CONVERSATION HISTORY (for context):");
                // Include up to 5 messages to provide compact history context (speeds up context pre-fill)
                int start = Math.Max(0, history.Count - 5);
                for (int i = start; i < history.Count; i++)
                {
                    string speaker = history[i].Sender.Equals("user", StringComparison.OrdinalIgnoreCase) ? "User" : "AI";
                    string text = history[i].Text;
                    // Truncate to 1000 characters to keep context small and fast
                    if (text.Length > 1000) text = text.Substring(0, 997) + "...";
                    historyBuilder.AppendLine($"- {speaker}: {text}");
                }
                historyBuilder.AppendLine();
            }

            var prompt = $@"You are AriyAI, an intelligent Manufacturing Assistant.
{historyBuilder}
The user asked: ""{message}""
We ran a query against the spreadsheet records and got these results:
{dataJson}

Write a natural, direct, and friendly English sentence that directly answers the user's question using the results and conversation history for context.
CRITICAL INSTRUCTIONS:
- Return ONLY the direct answer.
- Do NOT explain the SQL query, database structure, or how you calculated the result.
- Do NOT explain the query logic (e.g. do not mention GROUP BY, SUM, LIMIT, columns, tables).
- Keep it extremely concise (1 or 2 sentences max).
- If the data results contain both 'Customer' and 'Agent' roles for a name, clearly state the figures for BOTH roles in your response (e.g., 'AYUSH AGENCIES exists as both a Customer (generating INR 6,00,497) and an Agent (generating INR 5,13,822.55)').
- If numbers or currency are involved, ALWAYS format them strictly in INR (Indian Rupees, prefixed with INR, e.g. INR 15,43,200 or INR 3,00,78,616.71) with commas for thousands.
- Never use other currency symbols like dollars ($) or euros (€), even if the user used them in their query. Always output strictly in INR.
- If the results contain a list of items or names, list them clearly separated by commas in a single concise sentence.
- Example for lists: ""The agents are: N.JAYAPRAKASH, GM MARKETING, U. THALAIMALAI, STS MARKETING, AJITH, K. NAGANATHAN, and YESPEE ASSOCIATES.""
- Example for values: ""The top-selling product is 40s Combed Cotton Yarn with a total revenue of INR 15,43,200."" or ""The total sales across all records is INR 3,00,78,616.71.""

Direct Answer:";

            return await CallOllamaApiAsync(prompt, temperature: 0.2, numPredict: 150, cancellationToken: cancellationToken);
        }

        private async Task<string> CallOllamaApiAsync(string prompt, double temperature = 0.0, int numPredict = -1, int numCtx = 2048, CancellationToken cancellationToken = default)
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
                stream = false,
                options = new Dictionary<string, object>
                {
                    { "temperature", temperature },
                    { "num_predict", numPredict },
                    { "num_ctx", numCtx }
                }
            };

            string url = $"{baseUrl.TrimEnd('/')}/api/generate";
            var json = JsonSerializer.Serialize(requestBody);
            var httpContent = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync(url, httpContent, cancellationToken);
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

        private bool TryExtractSqlQuery(string text, out string sqlQuery)
        {
            sqlQuery = string.Empty;
            if (string.IsNullOrWhiteSpace(text)) return false;

            // 1. Check for markdown code blocks (```sql ... ``` or ``` ... ```)
            int codeBlockIndex = text.IndexOf("```");
            if (codeBlockIndex >= 0)
            {
                int start = codeBlockIndex + 3;
                if (text.Substring(start).StartsWith("sql", StringComparison.OrdinalIgnoreCase))
                {
                    start += 3;
                }
                
                int end = text.IndexOf("```", start);
                if (end > start)
                {
                    var content = text.Substring(start, end - start).Trim();
                    if (content.Contains("SELECT", StringComparison.OrdinalIgnoreCase))
                    {
                        sqlQuery = CleanSqlQuery(content);
                        return true;
                    }
                }
            }

            // 2. Fallback: Search for first "SELECT" and find "FROM"
            int selectIndex = text.IndexOf("SELECT", StringComparison.OrdinalIgnoreCase);
            if (selectIndex >= 0)
            {
                var remainder = text.Substring(selectIndex).Trim();
                int semicolonIndex = remainder.IndexOf(';');
                if (semicolonIndex >= 0)
                {
                    sqlQuery = remainder.Substring(0, semicolonIndex + 1).Trim();
                }
                else
                {
                    sqlQuery = CleanSqlQuery(remainder);
                }

                if (sqlQuery.Contains("FROM", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }
            }

            return false;
        }

        public class OllamaRequest
        {
            public string model { get; set; } = string.Empty;
            public string prompt { get; set; } = string.Empty;
            public bool stream { get; set; } = false;
            public Dictionary<string, object>? options { get; set; }
        }

        public class OllamaResponse
        {
            public string model { get; set; } = string.Empty;
            public string response { get; set; } = string.Empty;
            public bool done { get; set; }
        }

        public class ChartDatasetDto
        {
            public string Label { get; set; } = string.Empty;
            public List<double> Data { get; set; } = new();
            public List<string>? BackgroundColor { get; set; }
            public List<string>? BorderColor { get; set; }
            public double BorderWidth { get; set; } = 1;
            public bool Fill { get; set; } = false;
        }

        public class ChartConfigDto
        {
            public string Type { get; set; } = "bar";
            public List<string> Labels { get; set; } = new();
            public List<ChartDatasetDto> Datasets { get; set; } = new();
        }

        private static bool ContainsWholeWord(string text, string word)
        {
            if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(word)) return false;
            
            int index = 0;
            while ((index = text.IndexOf(word, index, StringComparison.OrdinalIgnoreCase)) != -1)
            {
                bool startBoundary = (index == 0 || !char.IsLetterOrDigit(text[index - 1]));
                bool endBoundary = (index + word.Length == text.Length || !char.IsLetterOrDigit(text[index + word.Length]));
                
                if (startBoundary && endBoundary)
                {
                    return true;
                }
                index += word.Length;
            }
            return false;
        }

        private ChartConfigDto? DetectAndBuildChart(string sqlQuery, List<Dictionary<string, object>> results, string userMessage)
        {
            if (results == null || results.Count < 2)
            {
                return null;
            }

            // Check if the user explicitly asked for visual charts/graphs/plots
            string msgLower = userMessage.ToLowerInvariant();
            bool askedForChart = msgLower.Contains("chart") || msgLower.Contains("graph") || 
                                 msgLower.Contains("plot") || msgLower.Contains("visual") || 
                                 msgLower.Contains("diagram") || msgLower.Contains("trend") ||
                                 msgLower.Contains("pie") || msgLower.Contains("donut") || 
                                 msgLower.Contains("bar") || msgLower.Contains("line");
                                 
            if (!askedForChart)
            {
                return null;
            }

            var firstRow = results[0];
            string? labelColumn = null;
            string? valueColumn = null;

            foreach (var kvp in firstRow)
            {
                var val = kvp.Value;
                if (val == null) continue;

                var colNameUpper = kvp.Key.ToUpperInvariant();
                bool isNumeric = val is short || val is int || val is long || val is float || val is double || val is decimal;

                if (!isNumeric && double.TryParse(val.ToString(), out _))
                {
                    isNumeric = true;
                }

                if (isNumeric)
                {
                    if (colNameUpper != "ID" && !colNameUpper.EndsWith("ID"))
                    {
                        valueColumn = kvp.Key;
                    }
                }
                else
                {
                    if (colNameUpper.Contains("NAME") || colNameUpper.Contains("DATE") || colNameUpper.Contains("ITEM") || colNameUpper.Contains("MONTH") || colNameUpper.Contains("ROLE"))
                    {
                        labelColumn = kvp.Key;
                    }
                }
            }

            if (string.IsNullOrEmpty(valueColumn))
            {
                foreach (var kvp in firstRow)
                {
                    var colNameUpper = kvp.Key.ToUpperInvariant();
                    if (colNameUpper == "ID" || colNameUpper.EndsWith("ID")) continue;

                    if (double.TryParse(kvp.Value?.ToString() ?? "", out _))
                    {
                        valueColumn = kvp.Key;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(labelColumn))
            {
                foreach (var kvp in firstRow)
                {
                    var colNameUpper = kvp.Key.ToUpperInvariant();
                    if (colNameUpper == "ID" || colNameUpper.EndsWith("ID")) continue;

                    if (!double.TryParse(kvp.Value?.ToString() ?? "", out _))
                    {
                        labelColumn = kvp.Key;
                        break;
                    }
                }
            }

            if (string.IsNullOrEmpty(labelColumn) || string.IsNullOrEmpty(valueColumn))
            {
                return null;
            }

            var labels = new List<string>();
            var dataValues = new List<double>();

            foreach (var row in results)
            {
                string labelVal = row.TryGetValue(labelColumn, out var lVal) && lVal != null ? lVal.ToString()! : "Unknown";
                if (DateTime.TryParse(labelVal, out var parsedDate))
                {
                    labelVal = parsedDate.ToString("yyyy-MM-dd");
                }

                double numVal = 0;
                if (row.TryGetValue(valueColumn, out var dVal) && dVal != null)
                {
                    double.TryParse(dVal.ToString(), out numVal);
                }

                labels.Add(labelVal);
                dataValues.Add(numVal);
            }

            string chartType = "bar";
            string queryUpper = sqlQuery.ToUpperInvariant();

            // Explicit chart type selection based on user request keywords
            if (ContainsWholeWord(userMessage, "pie") || 
                userMessage.Contains("piechart", StringComparison.OrdinalIgnoreCase) || 
                userMessage.Contains("piegraph", StringComparison.OrdinalIgnoreCase))
            {
                chartType = "pie";
            }
            else if (ContainsWholeWord(userMessage, "doughnut") || 
                     ContainsWholeWord(userMessage, "donut") || 
                     userMessage.Contains("doughnutchart", StringComparison.OrdinalIgnoreCase) || 
                     userMessage.Contains("donutchart", StringComparison.OrdinalIgnoreCase))
            {
                chartType = "doughnut";
            }
            else if (ContainsWholeWord(userMessage, "line") || 
                     userMessage.Contains("linechart", StringComparison.OrdinalIgnoreCase) || 
                     userMessage.Contains("linegraph", StringComparison.OrdinalIgnoreCase))
            {
                chartType = "line";
            }
            else if (ContainsWholeWord(userMessage, "bar") || 
                     ContainsWholeWord(userMessage, "column") || 
                     userMessage.Contains("barchart", StringComparison.OrdinalIgnoreCase) || 
                     userMessage.Contains("bargraph", StringComparison.OrdinalIgnoreCase) || 
                     userMessage.Contains("columnchart", StringComparison.OrdinalIgnoreCase) || 
                     userMessage.Contains("columngraph", StringComparison.OrdinalIgnoreCase))
            {
                chartType = "bar";
            }
            else
            {
                // Fall back to heuristics if no explicit choice
                bool isTimeTrend = false;
                if (!string.IsNullOrEmpty(labelColumn))
                {
                    string labelColUpper = labelColumn.ToUpperInvariant();
                    if (labelColUpper.Contains("DATE") || labelColUpper.Contains("MONTH") || labelColUpper.Contains("YEAR") || 
                        labelColUpper.Contains("WEEK") || labelColUpper.Contains("DAY") || labelColUpper.Contains("TIME"))
                    {
                        isTimeTrend = true;
                    }
                }

                if (queryUpper.Contains("TREND") || queryUpper.Contains("OVER TIME") || isTimeTrend)
                {
                    chartType = "line";
                }
                else if (results.Count <= 5 && (queryUpper.Contains("SHARE") || queryUpper.Contains("PERCENT") || queryUpper.Contains("DISTRIBUTION") || queryUpper.Contains("BREAKDOWN")))
                {
                    chartType = "doughnut";
                }
            }

            var dataset = new ChartDatasetDto
            {
                Label = valueColumn,
                Data = dataValues
            };

            if (chartType == "line")
            {
                dataset.Fill = true;
                dataset.BorderColor = new List<string> { "#1565c0" };
                dataset.BackgroundColor = new List<string> { "rgba(21, 101, 192, 0.08)" };
                dataset.BorderWidth = 2.5;
            }
            else if (chartType == "doughnut" || chartType == "pie")
            {
                dataset.BackgroundColor = new List<string>
                {
                    "#1a3a5c",
                    "#1565c0",
                    "#00bcd4",
                    "#26a69a",
                    "#ff9800",
                    "#9c27b0",
                    "#e91e63",
                    "#4caf50",
                    "#ffeb3b",
                    "#ff5722",
                    "#607d8b",
                    "#9e9e9e",
                    "#3f51b5"
                };
                dataset.BorderColor = new List<string> { "#ffffff" };
                dataset.BorderWidth = 1.5;
            }
            else
            {
                dataset.BackgroundColor = new List<string> { "rgba(21, 101, 192, 0.8)" };
                dataset.BorderColor = new List<string> { "#1565c0" };
                dataset.BorderWidth = 1;
            }

            return new ChartConfigDto
            {
                Type = chartType,
                Labels = labels,
                Datasets = new List<ChartDatasetDto> { dataset }
            };
        }
    }
}
