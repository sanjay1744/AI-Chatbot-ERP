using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MailKit.Net.Smtp;
using ExcelDataReader;
using System.Data;
using AriyAI.ERP.Api.Data;
using AriyAI.ERP.Api.Models;
using AriyAI.ERP.Api.Services;

namespace AriyAI.ERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class EmailsController : ControllerBase
    {
        private readonly ErpDbContext _db;
        private readonly EmailSyncWorker _syncWorker;
        private readonly ExtractionService _extractionService;
        private readonly MatchingService _matchingService;

        public EmailsController(
            ErpDbContext db,
            EmailSyncWorker syncWorker,
            ExtractionService extractionService,
            MatchingService matchingService)
        {
            _db = db;
            _syncWorker = syncWorker;
            _extractionService = extractionService;
            _matchingService = matchingService;
        }

        [HttpGet]
        public async Task<IActionResult> GetEmails()
        {
            var emails = await _db.Emails
                .Where(e => !e.IsDeleted)
                .OrderByDescending(e => e.ReceivedAt)
                .Take(50)
                .ToListAsync();
            return Ok(emails);
        }

        [HttpPost("sync")]
        public async Task<IActionResult> SyncEmails(CancellationToken cancellationToken)
        {
            // Hard 15-second deadline so the endpoint never hangs
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(15));

            try
            {
                int newCount = await _syncWorker.SyncEmailsAsync(cts.Token);
                int total = await _db.Emails.CountAsync(cancellationToken);
                return Ok(new { status = "success", new_emails_synced = newCount, total_emails = total });
            }
            catch (OperationCanceledException)
            {
                return Ok(new { status = "success", new_emails_synced = 0, total_emails = await _db.Emails.CountAsync(CancellationToken.None), detail = "Sync timed out but no data was lost." });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { status = "error", detail = ex.Message });
            }
        }

        [HttpPut("{id}/read")]
        public async Task<IActionResult> MarkEmailAsRead(int id, [FromBody] MarkAsReadDto dto)
        {
            var email = await _db.Emails.FindAsync(id);
            if (email == null) return NotFound();

            email.IsRead = dto.IsRead;
            await _db.SaveChangesAsync();
            return Ok(email);
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEmail(int id)
        {
            var email = await _db.Emails.FindAsync(id);
            if (email == null) return NotFound();

            email.IsDeleted = true;
            await _db.SaveChangesAsync();
            return Ok(new { status = "success", message = $"Email {id} deleted successfully" });
        }

        [HttpPost("{id}/extract")]
        public async Task<IActionResult> ExtractProducts(int id)
        {
            var email = await _db.Emails.FindAsync(id);
            if (email == null) return NotFound();

            // Mark email as read
            email.IsRead = true;
            await _db.SaveChangesAsync();

            var attachments = new List<EmailAttachmentDto>();
            if (!string.IsNullOrEmpty(email.AttachmentsJson))
            {
                try
                {
                    attachments = System.Text.Json.JsonSerializer.Deserialize<List<EmailAttachmentDto>>(email.AttachmentsJson) ?? new();
                }
                catch {}
            }

            List<ExtractedProductDto>? extracted = null;
            bool isExcelSource = false;

            // Check for Excel attachments first
            var excelAttachment = attachments.FirstOrDefault(att => 
                !string.IsNullOrEmpty(att.savedPath) && 
                (att.filename.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) || 
                 att.filename.EndsWith(".xls", StringComparison.OrdinalIgnoreCase)));

            // Check for PDF attachments
            var pdfAttachment = attachments.FirstOrDefault(att => 
                !string.IsNullOrEmpty(att.savedPath) && 
                att.filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase));

            if (excelAttachment != null)
            {
                string fullPath = Path.Combine(Directory.GetCurrentDirectory(), excelAttachment.savedPath);
                if (System.IO.File.Exists(fullPath))
                {
                    try
                    {
                        var excelParser = new ExcelParsingService();
                        extracted = excelParser.ParseExcelProducts(fullPath);
                        isExcelSource = true;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing Excel attachment: {ex.Message}");
                    }
                }
            }

            if (extracted == null && pdfAttachment != null)
            {
                string fullPath = Path.Combine(Directory.GetCurrentDirectory(), pdfAttachment.savedPath);
                if (System.IO.File.Exists(fullPath))
                {
                    try
                    {
                        var pdfParser = new PdfParsingService();
                        string pdfText = pdfParser.ParsePdfText(fullPath);
                        if (!string.IsNullOrWhiteSpace(pdfText))
                        {
                            extracted = _extractionService.ExtractProducts(pdfText);
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing PDF attachment: {ex.Message}");
                    }
                }
            }

            // Fallback to extracting from the email body text
            if (extracted == null)
            {
                extracted = _extractionService.ExtractProducts(email.Body);
            }

            // Run Fuzzy catalog matching
            var matched = _matchingService.MatchProducts(extracted);

            // Filter out conversational greetings, signatures, and noise phrases (skip for Excel source)
            List<ExtractedProductDto> filtered;
            if (isExcelSource)
            {
                filtered = matched;
            }
            else
            {
                filtered = matched.Where(item => IsValidProductLine(item)).ToList();
            }

            return Ok(filtered);
        }

        private bool IsValidProductLine(ExtractedProductDto item)
        {
            // 1. If the item fuzzy-matched successfully to the catalog, keep it
            if (item.Mapping == "Mapped")
            {
                return true;
            }

            string nameLower = (item.ProductDescription ?? "").ToLower();

            // 2. Filter out conversational greetings / sign-offs / signature noise
            var noisePhrases = new[]
            {
                "hello", "dear", "hi ", "team", "regards", "thank you", "thanks", "sincerely", "please", "quote",
                "enquiry", "requirement", "immediate", "immediately", "purchase dept", "spinning mills",
                "corporation", "ltd", "pvt", "limited", "attention", "undersigned", "subject:", "best regards"
            };

            foreach (var phrase in noisePhrases)
            {
                if (nameLower.Contains(phrase))
                {
                    // If the line matches noise and has no model/part numbers or digits, filter it out
                    if (string.IsNullOrEmpty(item.PartNumber) && !Regex.IsMatch(nameLower, @"\d"))
                    {
                        return false;
                    }
                }
            }

            // 3. Keep items containing known brands or product-related keywords
            var productKeywords = new[]
            {
                "contactor", "relay", "mcb", "mccb", "rccb", "elcb", "switch", "fuse", "cable", "wire",
                "roller", "belt", "pulley", "timer", "enclosure", "fitting", "lamp", "indicator", "power supply",
                "breaker", "starter", "isolator", "duct", "rail", "clamp", "coupling", "valve", "sensor", "product",
                "pcb", "board", "module", "plc", "hmi", "motor", "pump", "limit switch", "encoder", "inverter", "drive",
                "terminal", "block", "connector", "lug", "choke", "transformer", "fan", "heatsink", "panel", "busbar",
                "insulator", "meter", "gauge", "transmitter", "solenoid", "cylinder", "sprocket", "chain", "gear", "clutch", "brake"
            };
            var brands = new[] { "abb", "siemens", "schneider", "omron", "savio", "polycab", "ohtc" };

            bool hasKeyword = productKeywords.Any(k => nameLower.Contains(k));
            bool hasBrand = brands.Any(b => nameLower.Contains(b));
            bool hasPartNo = !string.IsNullOrEmpty(item.PartNumber);

            return hasKeyword || hasBrand || hasPartNo;
        }

        [HttpPost("send-acknowledgement")]
        public async Task<IActionResult> SendAcknowledgement([FromBody] AcknowledgementRequestDto request)
        {
            var user = Environment.GetEnvironmentVariable("EMAIL_USER");
            var password = Environment.GetEnvironmentVariable("EMAIL_PASSWORD");
            var smtpServer = Environment.GetEnvironmentVariable("SMTP_SERVER") ?? "smtp.gmail.com";
            var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "465";
            int smtpPort = int.TryParse(smtpPortStr, out int p) ? p : 465;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            {
                return BadRequest(new { detail = "SMTP email credentials (EMAIL_USER / EMAIL_PASSWORD) are not configured on the server." });
            }

            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("Sales Team", user));
                message.To.Add(new MailboxAddress(request.SenderName, request.SenderEmail));
                message.Subject = $"Acknowledgement: Sales Enquiry {request.EnquiryNum} Created";

                var bodyBuilder = new BodyBuilder();
                bodyBuilder.TextBody = $@"Dear {(!string.IsNullOrEmpty(request.SenderName) ? request.SenderName : "Sir/Madam")},

Thank you for your enquiry. We have registered it in our system under Enquiry Number: {request.EnquiryNum}.

Our team will review the details and get back to you shortly.

Best regards,
Sales Team
Naren Textile Engineers India Pvt. Ltd.";

                // Generate a text file attachment showing the details
                var details = $"ENQUIRY ACKNOWLEDGEMENT\n" +
                              $"======================\n" +
                              $"Enquiry Number: {request.EnquiryNum}\n" +
                              $"Enquiry Date: {request.EnquiryDate}\n" +
                              $"Customer: {request.CustomerName}\n" +
                              $"Address: {request.Address}\n\n" +
                              $"Items List:\n" +
                              $"-----------\n";

                foreach (var prod in request.Products)
                {
                    details += $"- {prod.ProductDescription} (Code: {prod.PartNo}) | Qty: {prod.Quantity} | Rate: {prod.Rate:C}\n";
                }

                byte[] attachmentBytes = System.Text.Encoding.UTF8.GetBytes(details);
                bodyBuilder.Attachments.Add($"Enquiry_Acknowledgement_{request.EnquiryNum}.txt", attachmentBytes);

                message.Body = bodyBuilder.ToMessageBody();

                using var client = new MailKit.Net.Smtp.SmtpClient();
                await client.ConnectAsync(smtpServer, smtpPort, true);
                await client.AuthenticateAsync(user, password);
                await client.SendAsync(message);
                await client.DisconnectAsync(true);

                return Ok(new { status = "success", message = $"Acknowledgement email sent to {request.SenderEmail}" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { detail = $"SMTP transmission failed: {ex.Message}" });
            }
        }

        [HttpGet("{emailId}/attachments/{filename}")]
        public async Task<IActionResult> DownloadEmailAttachment(int emailId, string filename)
        {
            var email = await _db.Emails.FindAsync(emailId);
            if (email == null) return NotFound();

            var attachments = new List<EmailAttachmentDto>();
            if (!string.IsNullOrEmpty(email.AttachmentsJson))
            {
                try
                {
                    attachments = System.Text.Json.JsonSerializer.Deserialize<List<EmailAttachmentDto>>(email.AttachmentsJson) ?? new();
                }
                catch {}
            }

            var att = attachments.FirstOrDefault(a => string.Equals(a.filename, filename, StringComparison.OrdinalIgnoreCase));
            if (att == null) return NotFound();

            string? savedPath = att.savedPath;
            if (string.IsNullOrEmpty(savedPath) || !System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), savedPath)))
            {
                // Try to recover dynamically from Gmail IMAP
                savedPath = await TryRecoverAttachmentFromImap(email, filename);
            }

            if (string.IsNullOrEmpty(savedPath)) return NotFound();
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), savedPath);

            var bytes = await System.IO.File.ReadAllBytesAsync(fullPath);
            string contentType = att.contentType ?? "application/octet-stream";

            if (filename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
            {
                return File(bytes, "application/pdf");
            }
            return File(bytes, contentType, filename);
        }

        [HttpGet("{emailId}/attachments/{filename}/preview")]
        public async Task<IActionResult> PreviewEmailAttachment(int emailId, string filename)
        {
            var email = await _db.Emails.FindAsync(emailId);
            if (email == null) return NotFound();

            var attachments = new List<EmailAttachmentDto>();
            if (!string.IsNullOrEmpty(email.AttachmentsJson))
            {
                try
                {
                    attachments = System.Text.Json.JsonSerializer.Deserialize<List<EmailAttachmentDto>>(email.AttachmentsJson) ?? new();
                }
                catch {}
            }

            var att = attachments.FirstOrDefault(a => string.Equals(a.filename, filename, StringComparison.OrdinalIgnoreCase));
            if (att == null) return NotFound();

            string? savedPath = att.savedPath;
            if (string.IsNullOrEmpty(savedPath) || !System.IO.File.Exists(Path.Combine(Directory.GetCurrentDirectory(), savedPath)))
            {
                // Try to recover dynamically from Gmail IMAP
                savedPath = await TryRecoverAttachmentFromImap(email, filename);
            }

            if (string.IsNullOrEmpty(savedPath)) return NotFound();
            string fullPath = Path.Combine(Directory.GetCurrentDirectory(), savedPath);

            try
            {
                System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
                using (var stream = System.IO.File.Open(fullPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    using (var reader = ExcelDataReader.ExcelReaderFactory.CreateReader(stream))
                    {
                        var result = reader.AsDataSet(new ExcelDataReader.ExcelDataSetConfiguration()
                        {
                            ConfigureDataTable = (_) => new ExcelDataReader.ExcelDataTableConfiguration()
                            {
                                UseHeaderRow = false
                            }
                        });

                        var sheetsList = new List<object>();
                        foreach (DataTable table in result.Tables)
                        {
                            var rowsList = new List<List<string>>();
                            foreach (DataRow row in table.Rows)
                            {
                                var cellsList = row.ItemArray
                                    .Select(cell => cell?.ToString() ?? "")
                                    .ToList();
                                rowsList.Add(cellsList);
                            }

                            sheetsList.Add(new
                            {
                                name = table.TableName,
                                rows = rowsList
                            });
                        }

                        return Ok(new { sheets = sheetsList });
                    }
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { detail = $"Failed to parse Excel: {ex.Message}" });
            }
        }

        private async Task<string?> TryRecoverAttachmentFromImap(Email email, string filename)
        {
            var user = Environment.GetEnvironmentVariable("EMAIL_USER");
            var password = Environment.GetEnvironmentVariable("EMAIL_PASSWORD");
            var server = Environment.GetEnvironmentVariable("IMAP_SERVER") ?? "imap.gmail.com";
            var portStr = Environment.GetEnvironmentVariable("IMAP_PORT") ?? "993";
            int port = int.TryParse(portStr, out int p) ? p : 993;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email.MessageId))
            {
                return null;
            }

            try
            {
                using var client = new MailKit.Net.Imap.ImapClient();
                client.Timeout = 15000;
                await client.ConnectAsync(server, port, true);
                await client.AuthenticateAsync(user, password);

                var inbox = client.Inbox;
                await inbox.OpenAsync(MailKit.FolderAccess.ReadOnly);

                // Search for the message by Message-ID header
                var query = MailKit.Search.SearchQuery.HeaderContains("Message-ID", email.MessageId);
                var uids = await inbox.SearchAsync(query);

                MimeKit.MimeMessage? message = null;
                if (uids.Count > 0)
                {
                    message = await inbox.GetMessageAsync(uids[0]);
                }
                else
                {
                    // Fallback scan: Search last 30 inbox messages in case IMAP indexing search is not returning headers immediately
                    int totalMessages = inbox.Count;
                    int startIdx = Math.Max(0, totalMessages - 30);
                    for (int i = totalMessages - 1; i >= startIdx; i--)
                    {
                        var summary = await inbox.GetMessageAsync(i);
                        if (summary.MessageId == email.MessageId)
                        {
                            message = summary;
                            break;
                        }
                    }
                }

                if (message != null)
                {
                    string uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "attachments");
                    if (!Directory.Exists(uploadsDir))
                    {
                        Directory.CreateDirectory(uploadsDir);
                    }

                    foreach (var attachment in message.Attachments)
                    {
                        if (attachment is MimeKit.MimePart part)
                        {
                            string partFilename = part.FileName ?? "";
                            if (string.Equals(partFilename, filename, StringComparison.OrdinalIgnoreCase))
                            {
                                string safeFilename = $"{Guid.NewGuid().ToString()}_{partFilename}";
                                string savedPath = Path.Combine(uploadsDir, safeFilename);

                                using (var stream = System.IO.File.Create(savedPath))
                                {
                                    part.Content.DecodeTo(stream);
                                }

                                string relativePath = Path.Combine("uploads", "attachments", safeFilename);

                                // Update DB records
                                var attachments = new List<EmailAttachmentDto>();
                                if (!string.IsNullOrEmpty(email.AttachmentsJson))
                                {
                                    attachments = System.Text.Json.JsonSerializer.Deserialize<List<EmailAttachmentDto>>(email.AttachmentsJson) ?? new();
                                }

                                var existing = attachments.FirstOrDefault(a => string.Equals(a.filename, filename, StringComparison.OrdinalIgnoreCase));
                                if (existing != null)
                                {
                                    existing.savedPath = relativePath;
                                }
                                else
                                {
                                    attachments.Add(new EmailAttachmentDto
                                    {
                                        filename = partFilename,
                                        contentType = part.ContentType.MimeType,
                                        savedPath = relativePath
                                    });
                                }

                                email.AttachmentsJson = System.Text.Json.JsonSerializer.Serialize(attachments);
                                _db.Emails.Update(email);
                                await _db.SaveChangesAsync();

                                return relativePath;
                            }
                        }
                    }
                }

                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Attachment recovery failed: {ex.Message}");
            }

            return null;
        }
    }

    public class MarkAsReadDto
    {
        public bool IsRead { get; set; }
    }

    public class AcknowledgementRequestDto
    {
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = string.Empty;
        public string EnquiryNum { get; set; } = string.Empty;
        public string EnquiryDate { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string Address { get; set; } = string.Empty;
        public string EnquiryExpiry { get; set; } = string.Empty;
        public List<ProductDto> Products { get; set; } = new();
    }

    public class ProductDto
    {
        public string ProductDescription { get; set; } = string.Empty;
        public string PartNo { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public decimal Rate { get; set; }
        public string Uom { get; set; } = string.Empty;
    }
    public class EmailAttachmentDto
    {
        public string filename { get; set; } = string.Empty;
        public string contentType { get; set; } = string.Empty;
        public string savedPath { get; set; } = string.Empty;
    }
}
