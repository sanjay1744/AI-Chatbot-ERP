using System.ComponentModel.DataAnnotations;

namespace AriyAI.ERP.Api.Models
{
    public class Agent
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Phone { get; set; } = string.Empty;

        [MaxLength(255)]
        public string? PasswordHash { get; set; }

        [MaxLength(255)]
        public string? SessionToken { get; set; }

        public DateTime? TokenExpiresAt { get; set; }
    }
}
