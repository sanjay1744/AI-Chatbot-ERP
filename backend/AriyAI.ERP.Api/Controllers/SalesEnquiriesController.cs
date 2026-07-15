using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AriyAI.ERP.Api.Data;
using AriyAI.ERP.Api.Models;
using MimeKit;
using MailKit.Net.Smtp;
using AriyAI.ERP.Api.Services;

namespace AriyAI.ERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesEnquiriesController : ControllerBase
    {
        private readonly ErpDbContext _context;

        public SalesEnquiriesController(ErpDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetEnquiries(
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? customerId = null,
            [FromQuery] string? query = null)
        {
            var dbQuery = _context.SalesEnquiries
                .Include(e => e.Customer)
                .Include(e => e.Agent)
                .Include(e => e.EnquiryProducts)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                dbQuery = dbQuery.Where(e => e.Status == status);
            }

            if (fromDate.HasValue)
            {
                dbQuery = dbQuery.Where(e => e.EnquiryDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                dbQuery = dbQuery.Where(e => e.EnquiryDate <= toDate.Value);
            }

            if (customerId.HasValue)
            {
                dbQuery = dbQuery.Where(e => e.CustomerId == customerId.Value);
            }

            if (!string.IsNullOrEmpty(query))
            {
                var q = query.ToLower();
                dbQuery = dbQuery.Where(e => e.EnquiryNumber.ToLower().Contains(q) || 
                                             (e.Customer != null && e.Customer.Name.ToLower().Contains(q)));
            }

            var enquiries = await dbQuery
                .OrderByDescending(e => e.EnquiryDate)
                .ToListAsync();

            // Project to match format shown in table grid (item count, customer details)
            return enquiries.Select(e => new
            {
                e.Id,
                e.EnquiryNumber,
                EnquiryDate = e.EnquiryDate.ToString("dd/MMM/yyyy"),
                Customer = e.Customer?.Name ?? string.Empty,
                Agent = e.Agent?.Name ?? "-",
                e.LeadType,
                City = e.Customer?.City ?? string.Empty,
                State = e.Customer?.State ?? string.Empty,
                e.Status,
                ItemsCount = e.EnquiryProducts.Count,
                e.Aging
            }).ToList<object>();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SalesEnquiry>> GetEnquiry(int id)
        {
            var enquiry = await _context.SalesEnquiries
                .Include(e => e.Customer)
                .Include(e => e.Agent)
                .Include(e => e.AssignedAgent)
                .Include(e => e.EnquiryProducts)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (enquiry == null)
            {
                return NotFound();
            }

            return enquiry;
        }

        [HttpPost]
        public async Task<ActionResult<SalesEnquiry>> CreateEnquiry(SalesEnquiry enquiry)
        {
            // Auto-generate Enquiry Number (e.g. ENQ072600124)
            var count = await _context.SalesEnquiries.CountAsync() + 1;
            enquiry.EnquiryNumber = $"ENQ{DateTime.Now:MMddyy}{count:D5}";
            enquiry.EnquiryDate = DateTime.Now;
            enquiry.Aging = 0;
            enquiry.Status = "Pending";

            if (enquiry.AgentId == null && enquiry.AssignToId != null)
            {
                enquiry.AgentId = enquiry.AssignToId;
            }

            _context.SalesEnquiries.Add(enquiry);
            await _context.SaveChangesAsync();

            if (enquiry.SourceEmailId.HasValue && enquiry.SourceEmailId.Value > 0)
            {
                // Run background task to send the email reply so it doesn't block the HTTP response!
                _ = Task.Run(async () =>
                {
                    try
                    {
                        using var scope = HttpContext.RequestServices.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();
                        
                        // Load full enquiry details (including customer and products)
                        var fullEnquiry = await db.SalesEnquiries
                            .Include(e => e.Customer)
                            .Include(e => e.EnquiryProducts)
                            .FirstOrDefaultAsync(e => e.Id == enquiry.Id);

                        if (fullEnquiry == null) return;

                        // Load original email
                        var origEmail = await db.Emails.FindAsync(enquiry.SourceEmailId.Value);
                        if (origEmail == null) return;

                        // Parse original sender email & name
                        var sender = origEmail.Sender;
                        var emailMatch = System.Text.RegularExpressions.Regex.Match(sender, @"<([^>]+)>");
                        var emailAddress = emailMatch.Success ? emailMatch.Groups[1].Value.Trim() : sender.Trim();
                        var nameMatch = System.Text.RegularExpressions.Regex.Match(sender, @"^([^<]+)");
                        var displayName = nameMatch.Success ? nameMatch.Groups[1].Value.Trim().Trim('"', '\'') : "";
                        if (string.IsNullOrEmpty(displayName))
                        {
                            displayName = fullEnquiry.Customer?.Name ?? "Customer";
                        }

                        // Generate PDF acknowledgement
                        byte[] pdfBytes;
                        try
                        {
                            pdfBytes = EnquiryPdfGenerator.GenerateAcknowledgementPdf(fullEnquiry);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error generating PDF acknowledgement: {ex.Message}");
                            // Fallback to text format if PDF generation fails
                            string details = $"ENQUIRY ACKNOWLEDGEMENT\n" +
                                             $"======================\n" +
                                             $"Enquiry Number: {fullEnquiry.EnquiryNumber}\n" +
                                             $"Date: {fullEnquiry.EnquiryDate}\n" +
                                             $"Customer: {fullEnquiry.Customer?.Name}\n\n" +
                                             "Items:\n";
                            foreach (var prod in fullEnquiry.EnquiryProducts)
                            {
                                details += $"- {prod.ProductDescription} | Qty: {prod.Quantity}\n";
                            }
                            pdfBytes = System.Text.Encoding.UTF8.GetBytes(details);
                        }

                        // Get SMTP settings from env
                        var user = Environment.GetEnvironmentVariable("EMAIL_USER");
                        var password = Environment.GetEnvironmentVariable("EMAIL_PASSWORD");
                        var smtpServer = Environment.GetEnvironmentVariable("SMTP_SERVER") ?? "smtp.gmail.com";
                        var smtpPortStr = Environment.GetEnvironmentVariable("SMTP_PORT") ?? "465";
                        int smtpPort = int.TryParse(smtpPortStr, out int p) ? p : 465;

                        if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
                        {
                            Console.WriteLine("SMTP email credentials not configured in environment. Cannot send reply.");
                            return;
                        }

                        var message = new MimeMessage();
                        message.From.Add(new MailboxAddress("Sales Team", user));
                        message.To.Add(new MailboxAddress(displayName, emailAddress));

                        // Subject: match original subject prefixed with "Re: " to thread under the same email
                        var replySubject = origEmail.Subject ?? "";
                        if (!replySubject.StartsWith("Re:", StringComparison.OrdinalIgnoreCase))
                        {
                            replySubject = "Re: " + replySubject;
                        }
                        message.Subject = replySubject;

                        // Threading headers
                        if (!string.IsNullOrEmpty(origEmail.MessageId))
                        {
                            var cleanMsgId = origEmail.MessageId;
                            if (!cleanMsgId.StartsWith("<")) cleanMsgId = $"<{cleanMsgId}>";
                            message.Headers.Add("In-Reply-To", cleanMsgId);
                            message.Headers.Add("References", cleanMsgId);
                        }

                        var bodyBuilder = new BodyBuilder();
                        bodyBuilder.TextBody = $@"Dear {displayName},

Thank you for your enquiry. We have registered it in our system under Enquiry Number: {fullEnquiry.EnquiryNumber}.

Please find attached a copy of the enquiry details for your reference. Our team will review the details and get back to you shortly.

Best regards,
Sales Team
Naren Textile Engineers India Pvt. Ltd.";

                        // Attach the PDF acknowledgement
                        bodyBuilder.Attachments.Add($"Enquiry_Acknowledgement_{fullEnquiry.EnquiryNumber}.pdf", pdfBytes, new ContentType("application", "pdf"));

                        message.Body = bodyBuilder.ToMessageBody();

                        using var client = new MailKit.Net.Smtp.SmtpClient();
                        await client.ConnectAsync(smtpServer, smtpPort, true);
                        await client.AuthenticateAsync(user, password);
                        await client.SendAsync(message);
                        await client.DisconnectAsync(true);

                        Console.WriteLine($"Acknowledgement reply sent to {emailAddress} for enquiry {fullEnquiry.EnquiryNumber}");
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Failed to send enquiry reply email: {ex.Message}");
                    }
                });
            }

            return CreatedAtAction(nameof(GetEnquiry), new { id = enquiry.Id }, enquiry);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEnquiry(int id, SalesEnquiry enquiry)
        {
            if (id != enquiry.Id)
            {
                return BadRequest();
            }

            var existing = await _context.SalesEnquiries
                .Include(e => e.EnquiryProducts)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            // Update scalar fields
            existing.CustomerId = enquiry.CustomerId;
            existing.AgentId = enquiry.AgentId ?? enquiry.AssignToId;
            existing.Source = enquiry.Source;
            existing.LeadType = enquiry.LeadType;
            existing.Address = enquiry.Address;
            existing.AssignToId = enquiry.AssignToId;
            existing.ExpiryDate = enquiry.ExpiryDate;
            existing.CustomerCountry = enquiry.CustomerCountry;
            existing.Remarks = enquiry.Remarks;
            existing.Status = enquiry.Status;

            // Simple replace of products
            _context.EnquiryProducts.RemoveRange(existing.EnquiryProducts);
            existing.EnquiryProducts = enquiry.EnquiryProducts;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EnquiryExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEnquiry(int id)
        {
            var enquiry = await _context.SalesEnquiries.FindAsync(id);
            if (enquiry == null)
            {
                return NotFound();
            }

            _context.SalesEnquiries.Remove(enquiry);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool EnquiryExists(int id)
        {
            return _context.SalesEnquiries.Any(e => e.Id == id);
        }
    }
}
