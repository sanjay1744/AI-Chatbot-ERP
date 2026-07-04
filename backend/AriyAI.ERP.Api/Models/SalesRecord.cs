using System;
using System.ComponentModel.DataAnnotations;

namespace AriyAI.ERP.Api.Models
{
    public class SalesRecord
    {
        public int Id { get; set; }
        
        public DateTime InvoiceDate { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string InvoiceNumber { get; set; } = string.Empty;
        
        public int CustomerId { get; set; }
        
        [Required]
        [MaxLength(200)]
        public string CustomerName { get; set; } = string.Empty;
        
        public int ItemId { get; set; }
        
        [Required]
        [MaxLength(250)]
        public string ItemName { get; set; } = string.Empty;
        
        [MaxLength(50)]
        public string Uom { get; set; } = string.Empty;
        
        public int Qty { get; set; }
        
        public decimal Rate { get; set; }
        
        public int AgentId { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string AgentName { get; set; } = string.Empty;
    }
}
