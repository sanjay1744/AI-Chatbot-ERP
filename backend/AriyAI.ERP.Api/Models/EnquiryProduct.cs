using System.ComponentModel.DataAnnotations;

namespace AriyAI.ERP.Api.Models
{
    public class EnquiryProduct
    {
        public int Id { get; set; }

        public int SalesEnquiryId { get; set; }

        public int? ProductId { get; set; }
        
        [MaxLength(100)]
        public string Group { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string ProductDescription { get; set; } = string.Empty;

        [MaxLength(100)]
        public string PartNumber { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Make { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Model { get; set; } = string.Empty;

        public int Quantity { get; set; }

        public decimal Rate { get; set; }
    }
}
