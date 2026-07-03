using System;
using System.ComponentModel.DataAnnotations;

namespace AriyAI.ERP.Api.Models
{
    public class ChatMessage
    {
        public int Id { get; set; }

        public int ChatSessionId { get; set; }

        [Required]
        [MaxLength(50)]
        public string Sender { get; set; } = string.Empty; // "user" or "ai"

        [Required]
        public string Text { get; set; } = string.Empty;

        public string? Sql { get; set; }

        public string? Data { get; set; } // JSON-serialized string

        public string? Chart { get; set; } // JSON-serialized string

        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }
}
