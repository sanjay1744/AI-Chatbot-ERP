using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using MailKit.Net.Imap;
using MailKit.Net.Smtp;
using AriyAI.ERP.Api.Data;
using AriyAI.ERP.Api.Models;
using AriyAI.ERP.Api.Filters;
using AriyAI.ERP.Api.Services;

namespace AriyAI.ERP.Api.Controllers
{
    [ApiController]
    [Route("api/email-settings")]
    [ServiceFilter(typeof(AgentAuthFilter))]
    public class AgentEmailSettingsController : ControllerBase
    {
        private readonly ErpDbContext _db;

        public AgentEmailSettingsController(ErpDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetSettings()
        {
            var agent = HttpContext.Items["CurrentAgent"] as Agent;
            if (agent == null) return Unauthorized();

            var config = await _db.AgentEmailConfigurations
                .FirstOrDefaultAsync(c => c.AgentId == agent.Id);

            if (config == null)
            {
                return Ok(new
                {
                    imapServer = "imap.gmail.com",
                    imapPort = 993,
                    imapUsername = "",
                    imapPassword = "",
                    smtpServer = "smtp.gmail.com",
                    smtpPort = 465,
                    smtpUsername = "",
                    smtpPassword = "",
                    useSsl = true,
                    configured = false
                });
            }

            return Ok(new
            {
                imapServer = config.ImapServer,
                imapPort = config.ImapPort,
                imapUsername = config.ImapUsername,
                imapPassword = string.IsNullOrEmpty(config.ImapPassword) ? "" : "********",
                smtpServer = config.SmtpServer,
                smtpPort = config.SmtpPort,
                smtpUsername = config.SmtpUsername,
                smtpPassword = string.IsNullOrEmpty(config.SmtpPassword) ? "" : "********",
                useSsl = config.UseSsl,
                configured = true,
                lastSyncedAt = config.LastSyncedAt
            });
        }

        /// <summary>
        /// Validates that a server hostname looks reasonable before attempting a slow network connection.
        /// Returns null if valid, or an error message string if invalid.
        /// </summary>
        private static string? ValidateHostname(string hostname, string label)
        {
            if (string.IsNullOrWhiteSpace(hostname))
                return $"{label} server hostname is required.";
            if (hostname.Contains('@'))
                return $"{label} server should be a hostname (e.g. imap.gmail.com), not an email address.";
            if (!hostname.Contains('.'))
                return $"{label} server \"{hostname}\" doesn't look like a valid hostname. Expected something like imap.gmail.com or smtp.gmail.com.";
            return null;
        }

        [HttpPost]
        public async Task<IActionResult> SaveSettings([FromBody] SaveEmailSettingsDto dto)
        {
            var agent = HttpContext.Items["CurrentAgent"] as Agent;
            if (agent == null) return Unauthorized();

            if (dto == null) return BadRequest("Settings data is required.");

            // Validate hostnames before saving
            var imapErr = ValidateHostname(dto.ImapServer, "IMAP");
            if (imapErr != null) return BadRequest(new { detail = imapErr });

            var smtpErr = ValidateHostname(dto.SmtpServer, "SMTP");
            if (smtpErr != null) return BadRequest(new { detail = smtpErr });

            var config = await _db.AgentEmailConfigurations
                .FirstOrDefaultAsync(c => c.AgentId == agent.Id);

            bool isNew = false;
            if (config == null)
            {
                config = new AgentEmailConfiguration { AgentId = agent.Id };
                isNew = true;
            }

            config.ImapServer = dto.ImapServer;
            config.ImapPort = dto.ImapPort;
            config.ImapUsername = dto.ImapUsername;
            
            // Only update IMAP password if it was edited
            if (dto.ImapPassword != "********" && !string.IsNullOrEmpty(dto.ImapPassword))
            {
                config.ImapPassword = CryptographyHelper.Encrypt(dto.ImapPassword);
            }

            config.SmtpServer = dto.SmtpServer;
            config.SmtpPort = dto.SmtpPort;
            config.SmtpUsername = dto.SmtpUsername;

            // Only update SMTP password if it was edited
            if (dto.SmtpPassword != "********" && !string.IsNullOrEmpty(dto.SmtpPassword))
            {
                config.SmtpPassword = CryptographyHelper.Encrypt(dto.SmtpPassword);
            }

            config.UseSsl = dto.UseSsl;

            if (isNew)
            {
                _db.AgentEmailConfigurations.Add(config);
            }
            else
            {
                _db.AgentEmailConfigurations.Update(config);
            }

            await _db.SaveChangesAsync();

            return Ok(new { status = "success", detail = "Email configuration saved successfully." });
        }

        [HttpPost("test")]
        public async Task<IActionResult> TestSettings([FromBody] SaveEmailSettingsDto dto)
        {
            var agent = HttpContext.Items["CurrentAgent"] as Agent;
            if (agent == null) return Unauthorized();

            if (dto == null) return BadRequest("Settings data is required.");

            // Validate hostnames instantly before attempting slow network connections
            var imapErr = ValidateHostname(dto.ImapServer, "IMAP");
            if (imapErr != null) return BadRequest(new { detail = imapErr });

            var smtpErr = ValidateHostname(dto.SmtpServer, "SMTP");
            if (smtpErr != null) return BadRequest(new { detail = smtpErr });

            string imapPassword = dto.ImapPassword;
            string smtpPassword = dto.SmtpPassword;

            // Restore password from DB if it is masked
            if (dto.ImapPassword == "********" || dto.SmtpPassword == "********")
            {
                var savedConfig = await _db.AgentEmailConfigurations
                    .FirstOrDefaultAsync(c => c.AgentId == agent.Id);
                if (savedConfig != null)
                {
                    if (dto.ImapPassword == "********")
                        imapPassword = CryptographyHelper.Decrypt(savedConfig.ImapPassword);
                    if (dto.SmtpPassword == "********")
                        smtpPassword = CryptographyHelper.Decrypt(savedConfig.SmtpPassword);
                }
            }

            // Test IMAP Connection
            try
            {
                using var client = new ImapClient();
                client.Timeout = 5000; // 5-second timeout
                await client.ConnectAsync(dto.ImapServer, dto.ImapPort, dto.UseSsl);
                await client.AuthenticateAsync(dto.ImapUsername, imapPassword);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                return BadRequest(new { detail = $"IMAP Connection Failed: {ex.Message}" });
            }

            // Test SMTP Connection
            try
            {
                using var client = new SmtpClient();
                client.Timeout = 5000; // 5-second timeout
                await client.ConnectAsync(dto.SmtpServer, dto.SmtpPort, dto.UseSsl);
                await client.AuthenticateAsync(dto.SmtpUsername, smtpPassword);
                await client.DisconnectAsync(true);
            }
            catch (Exception ex)
            {
                return BadRequest(new { detail = $"SMTP Connection Failed: {ex.Message}" });
            }

            return Ok(new { status = "success", detail = "IMAP & SMTP configurations verified successfully!" });
        }
    }

    public class SaveEmailSettingsDto
    {
        public string ImapServer { get; set; } = string.Empty;
        public int ImapPort { get; set; } = 993;
        public string ImapUsername { get; set; } = string.Empty;
        public string ImapPassword { get; set; } = string.Empty;
        public string SmtpServer { get; set; } = string.Empty;
        public int SmtpPort { get; set; } = 465;
        public string SmtpUsername { get; set; } = string.Empty;
        public string SmtpPassword { get; set; } = string.Empty;
        public bool UseSsl { get; set; } = true;
    }
}
