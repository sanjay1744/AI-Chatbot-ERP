using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AriyAI.ERP.Api.Models
{
    [Table("AgentEmailConfigurations")]
    public class AgentEmailConfiguration
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        public int AgentId { get; set; }

        [ForeignKey("AgentId")]
        public Agent? Agent { get; set; }

        [Required]
        [MaxLength(255)]
        public string ImapServer { get; set; } = "imap.gmail.com";

        [Required]
        public int ImapPort { get; set; } = 993;

        [Required]
        [MaxLength(255)]
        public string ImapUsername { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string ImapPassword { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string SmtpServer { get; set; } = "smtp.gmail.com";

        [Required]
        public int SmtpPort { get; set; } = 465;

        [Required]
        [MaxLength(255)]
        public string SmtpUsername { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string SmtpPassword { get; set; } = string.Empty;

        [Required]
        public bool UseSsl { get; set; } = true;

        public DateTime? LastSyncedAt { get; set; }
    }
}
