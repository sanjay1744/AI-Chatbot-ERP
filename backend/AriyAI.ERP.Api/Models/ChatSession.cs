using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AriyAI.ERP.Api.Models
{
    public class ChatSession
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = "New Chat";

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

        public List<ChatMessage> Messages { get; set; } = new();
    }
}
