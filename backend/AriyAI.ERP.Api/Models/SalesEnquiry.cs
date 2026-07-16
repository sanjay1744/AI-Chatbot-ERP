using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AriyAI.ERP.Api.Models
{
    public class SalesEnquiry
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(50)]
        public string EnquiryNumber { get; set; } = string.Empty;

        public DateTime EnquiryDate { get; set; } = DateTime.Now;

        public int CustomerId { get; set; }
        public Customer? Customer { get; set; }

        public int? AgentId { get; set; }
        public Agent? Agent { get; set; }

        [MaxLength(100)]
        public string Source { get; set; } = string.Empty;

        [MaxLength(50)]
        public string LeadType { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Address { get; set; } = string.Empty;

        public int? AssignToId { get; set; }
        public Agent? AssignedAgent { get; set; }

        public DateTime? ExpiryDate { get; set; }

        [MaxLength(100)]
        public string CustomerCountry { get; set; } = string.Empty;

        [MaxLength(500)]
        public string Remarks { get; set; } = string.Empty;

        [MaxLength(50)]
        public string Status { get; set; } = "Pending";

        public int Aging { get; set; }

        public int? SourceEmailId { get; set; }

        public List<EnquiryProduct> EnquiryProducts { get; set; } = new();
    }
}
