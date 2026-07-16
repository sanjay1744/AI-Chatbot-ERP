using System.ComponentModel.DataAnnotations;

namespace AriyAI.ERP.Api.Models
{
    public class PotentialItem
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(250)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(100)]
        public string PartNumber { get; set; } = string.Empty;

        public decimal Rate { get; set; }
    }
}
