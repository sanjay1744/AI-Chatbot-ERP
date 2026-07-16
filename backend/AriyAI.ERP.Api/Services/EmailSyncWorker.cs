using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MailKit;
using MailKit.Net.Imap;
using MailKit.Search;
using MimeKit;
using AriyAI.ERP.Api.Data;
using AriyAI.ERP.Api.Models;

namespace AriyAI.ERP.Api.Services
{
    public class EmailSyncWorker : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<EmailSyncWorker> _logger;

        public EmailSyncWorker(
            IServiceProvider serviceProvider,
            ILogger<EmailSyncWorker> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Background Email Synchronization loop started (runs every 2 minutes).");

            // Startup delay
            await Task.Delay(5000, stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Running scheduled Gmail IMAP synchronization...");
                    await SyncEmailsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during background email synchronization.");
                }

                await Task.Delay(TimeSpan.FromSeconds(120), stoppingToken);
            }
        }

        public async Task<int> SyncEmailsAsync(CancellationToken cancellationToken)
        {
            var user = Environment.GetEnvironmentVariable("EMAIL_USER");
            var password = Environment.GetEnvironmentVariable("EMAIL_PASSWORD");
            var server = Environment.GetEnvironmentVariable("IMAP_SERVER") ?? "imap.gmail.com";
            var portStr = Environment.GetEnvironmentVariable("IMAP_PORT") ?? "993";
            int port = int.TryParse(portStr, out int p) ? p : 993;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("Email credentials (EMAIL_USER / EMAIL_PASSWORD) not configured. Skipping sync.");
                return 0;
            }

            using var client = new ImapClient();
            client.Timeout = 20000; // 20 seconds timeout to prevent infinite hangs
            
            // Allow SSL connection
            await client.ConnectAsync(server, port, true, cancellationToken);
            await client.AuthenticateAsync(user, password, cancellationToken);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            int totalMessages = inbox.Count;
            if (totalMessages == 0)
            {
                await client.DisconnectAsync(true, cancellationToken);
                return 0;
            }

            // Fetch last 20 messages using index-based range Fetch instead of SearchQuery.All (which downloads all UIDs)
            int startIdx = Math.Max(0, totalMessages - 20);
            int endIdx = totalMessages - 1;
            var summaries = await inbox.FetchAsync(startIdx, endIdx, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId, cancellationToken);

            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

            int newEmailsCount = 0;

            // Load filters
            var filterStr = Environment.GetEnvironmentVariable("EMAIL_FILTER_KEYWORDS") ?? "enquiry,quotation,sales order,products,product list,product lists";
            var keywords = filterStr.Split(',')
                .Select(k => k.Trim().ToLower())
                .Where(k => !string.IsNullOrEmpty(k))
                .ToList();

            // Process summaries
            foreach (var summary in summaries)
            {
                var envelope = summary.Envelope;
                if (envelope == null) continue;

                var messageId = envelope.MessageId?.Trim('<', '>', ' ', '\t') ?? $"fallback-{envelope.Date.Value.Ticks}";

                // Idempotency Check
                bool exists = db.Emails.Any(e => e.MessageId == messageId);
                if (exists) continue;

                // Download full message content only if not already saved
                var message = await inbox.GetMessageAsync(summary.UniqueId, cancellationToken);
                string subject = envelope.Subject ?? "(No Subject)";
                string body = ExtractTextBody(message);

                // Apply Subject Filters
                string subjectLower = subject.ToLower();
                if (keywords.Count > 0 && !keywords.Any(kw => subjectLower.Contains(kw)))
                {
                    continue; // Skip emails that do not match filters
                }

                var emailEntity = new Email
                {
                    MessageId = messageId,
                    Sender = envelope.From.ToString(),
                    Recipient = envelope.To.ToString(),
                    Subject = subject,
                    Body = body,
                    AttachmentsJson = GetAttachmentsJson(message),
                    ReceivedAt = envelope.Date?.DateTime ?? DateTime.UtcNow,
                    IsRead = false,
                    IsDeleted = false
                };

                db.Emails.Add(emailEntity);
                newEmailsCount++;
            }

            if (newEmailsCount > 0)
            {
                await db.SaveChangesAsync(cancellationToken);
                _logger.LogInformation("Successfully synced {Count} new emails.", newEmailsCount);
            }
            else
            {
                _logger.LogInformation("Synchronization complete. No new emails found.");
            }

            await client.DisconnectAsync(true, cancellationToken);
            return newEmailsCount;
        }

        private string ExtractTextBody(MimeMessage message)
        {
            if (!string.IsNullOrEmpty(message.TextBody))
            {
                return message.TextBody;
            }
            if (!string.IsNullOrEmpty(message.HtmlBody))
            {
                var rawText = Regex.Replace(message.HtmlBody, "<[^>]+>", "");
                return System.Net.WebUtility.HtmlDecode(rawText).Trim();
            }
            return string.Empty;
        }

        private string GetAttachmentsJson(MimeMessage message)
        {
            var list = new List<object>();
            string uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "attachments");
            try
            {
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to create uploads directory {Path}", uploadsDir);
            }

            foreach (var attachment in message.Attachments)
            {
                if (attachment is MimePart part)
                {
                    string filename = part.FileName ?? $"attachment-{Guid.NewGuid().ToString().Substring(0, 6)}";
                    string ext = Path.GetExtension(filename).ToLowerInvariant();

                    if (ext == ".xlsx" || ext == ".xls" || ext == ".pdf")
                    {
                        try
                        {
                            string safeFilename = $"{Guid.NewGuid().ToString()}_{filename}";
                            string savedPath = Path.Combine(uploadsDir, safeFilename);

                            using (var stream = File.Create(savedPath))
                            {
                                part.Content.DecodeTo(stream);
                            }

                            list.Add(new { 
                                filename = filename, 
                                contentType = part.ContentType.MimeType,
                                savedPath = Path.Combine("uploads", "attachments", safeFilename)
                            });
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to save attachment {Filename}", filename);
                            list.Add(new { filename = filename, contentType = part.ContentType.MimeType });
                        }
                    }
                    else
                    {
                        list.Add(new { filename = filename, contentType = part.ContentType.MimeType });
                    }
                }
            }
            return System.Text.Json.JsonSerializer.Serialize(list);
        }
    }
}
