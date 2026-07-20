using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AriyAI.ERP.Api.Models
{
    [Table("Emails")]
    public class Email
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int Id { get; set; }

        [Required]
        [MaxLength(255)]
        public string MessageId { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Sender { get; set; } = string.Empty;

        [Required]
        [MaxLength(255)]
        public string Recipient { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Subject { get; set; } = string.Empty;

        [Required]
        public string Body { get; set; } = string.Empty;

        [Required]
        public string AttachmentsJson { get; set; } = "[]";

        [Required]
        public DateTime ReceivedAt { get; set; }

        [Required]
        public bool IsRead { get; set; } = false;

        [Required]
        public bool IsDeleted { get; set; } = false;

        public int? AgentId { get; set; }

        [ForeignKey("AgentId")]
        public Agent? Agent { get; set; }
    }
}
