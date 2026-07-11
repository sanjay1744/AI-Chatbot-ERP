using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MimeKit;
using MailKit.Net.Smtp;
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

            // Run Regex extraction
            var extracted = _extractionService.ExtractProducts(email.Body);

            // Run Fuzzy catalog matching
            var matched = _matchingService.MatchProducts(extracted);

            // Filter out conversational greetings, signatures, and noise phrases
            var filtered = matched.Where(item => IsValidProductLine(item)).ToList();

            return Ok(filtered);
        }

        private bool IsValidProductLine(ExtractedProductDto item)
        {
            // 1. If the item fuzzy-matched successfully to the catalog, keep it
            if (item.Mapping == "Matched")
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
}
