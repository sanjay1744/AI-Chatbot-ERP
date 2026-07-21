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
using Microsoft.EntityFrameworkCore;
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

        public async Task<int> SyncEmailsAsync(CancellationToken cancellationToken, int? agentId = null)
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ErpDbContext>();

            if (agentId.HasValue)
            {
                var config = await db.AgentEmailConfigurations
                    .FirstOrDefaultAsync(c => c.AgentId == agentId.Value, cancellationToken);

                if (config == null)
                {
                    _logger.LogWarning("Email configuration not found for Agent ID: {AgentId}. Skipping sync.", agentId.Value);
                    return 0;
                }

                int count = await SyncForConfigurationAsync(db, config, cancellationToken);
                if (count > 0)
                {
                    config.LastSyncedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync(cancellationToken);
                }
                return count;
            }
            else
            {
                var configs = await db.AgentEmailConfigurations.ToListAsync(cancellationToken);
                int totalSynced = 0;
                foreach (var config in configs)
                {
                    try
                    {
                        int count = await SyncForConfigurationAsync(db, config, cancellationToken);
                        if (count > 0)
                        {
                            config.LastSyncedAt = DateTime.UtcNow;
                            totalSynced += count;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error occurred during background email sync for Agent ID: {AgentId} ({Username})", config.AgentId, config.ImapUsername);
                    }
                }
                if (totalSynced > 0)
                {
                    await db.SaveChangesAsync(cancellationToken);
                }
                return totalSynced;
            }
        }

        private async Task<int> SyncForConfigurationAsync(ErpDbContext db, AgentEmailConfiguration config, CancellationToken cancellationToken)
        {
            var user = config.ImapUsername;
            var password = CryptographyHelper.Decrypt(config.ImapPassword);
            var server = config.ImapServer;
            var port = config.ImapPort;
            var useSsl = config.UseSsl;

            if (string.IsNullOrEmpty(user) || string.IsNullOrEmpty(password))
            {
                _logger.LogWarning("Email credentials for Agent ID {AgentId} are incomplete. Skipping sync.", config.AgentId);
                return 0;
            }

            using var client = new ImapClient();
            client.Timeout = 20000; // 20 seconds timeout to prevent infinite hangs
            
            await client.ConnectAsync(server, port, useSsl, cancellationToken);
            await client.AuthenticateAsync(user, password, cancellationToken);

            var inbox = client.Inbox;
            await inbox.OpenAsync(FolderAccess.ReadOnly, cancellationToken);

            int totalMessages = inbox.Count;
            if (totalMessages == 0)
            {
                await client.DisconnectAsync(true, cancellationToken);
                return 0;
            }

            // Fetch last 20 messages using index-based range Fetch
            int startIdx = Math.Max(0, totalMessages - 20);
            int endIdx = totalMessages - 1;
            var summaries = await inbox.FetchAsync(startIdx, endIdx, MessageSummaryItems.Envelope | MessageSummaryItems.UniqueId, cancellationToken);

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

                var messageId = envelope.MessageId?.Trim('<', '>', ' ', '\t') ?? $"fallback-{(envelope.Date?.Ticks ?? DateTimeOffset.UtcNow.Ticks)}-{summary.UniqueId}";

                // Idempotency Check scoped per Agent
                bool exists = db.Emails.Any(e => e.MessageId == messageId && e.AgentId == config.AgentId);
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
                    IsDeleted = false,
                    AgentId = config.AgentId // Set owner agent
                };

                db.Emails.Add(emailEntity);
                newEmailsCount++;
            }

            if (newEmailsCount > 0)
            {
                _logger.LogInformation("Successfully synced {Count} new emails for Agent {AgentId}.", newEmailsCount, config.AgentId);
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
