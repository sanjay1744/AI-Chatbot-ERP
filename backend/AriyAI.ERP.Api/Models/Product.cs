using System.ComponentModel.DataAnnotations;

namespace AriyAI.ERP.Api.Models
{
    public class Product
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Group { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string Description { get; set; } = string.Empty;

        [MaxLength(100)]
        public string PartNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Make { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Model { get; set; } = string.Empty;

        public decimal Rate { get; set; }
    }
}
