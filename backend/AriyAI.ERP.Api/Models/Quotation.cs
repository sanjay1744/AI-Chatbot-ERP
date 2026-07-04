using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AriyAI.ERP.Api.Models
{
    public class Quotation
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string QuotationNumber { get; set; } = string.Empty;

        public DateTime QuotationDate { get; set; } = DateTime.Now;

        [MaxLength(100)]
        public string CustomerReference { get; set; } = string.Empty;

        [MaxLength(10)]
        public string Currency { get; set; } = "INR";

        public DateTime DueDate { get; set; } = DateTime.Now.AddDays(30);

        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        public int? AgentId { get; set; }
        public Agent? Agent { get; set; }

        [MaxLength(250)]
        public string Subject1 { get; set; } = string.Empty;

        [MaxLength(250)]
        public string Subject2 { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public int Aging { get; set; }

        public List<QuotationProduct> QuotationProducts { get; set; } = new();
    }
}
