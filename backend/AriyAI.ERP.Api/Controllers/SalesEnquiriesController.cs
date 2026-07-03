using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AriyAI.ERP.Api.Data;
using AriyAI.ERP.Api.Models;

namespace AriyAI.ERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SalesEnquiriesController : ControllerBase
    {
        private readonly ErpDbContext _context;

        public SalesEnquiriesController(ErpDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetEnquiries(
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? customerId = null,
            [FromQuery] string? query = null)
        {
            var dbQuery = _context.SalesEnquiries
                .Include(e => e.Customer)
                .Include(e => e.Agent)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                dbQuery = dbQuery.Where(e => e.Status == status);
            }

            if (fromDate.HasValue)
            {
                dbQuery = dbQuery.Where(e => e.EnquiryDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                dbQuery = dbQuery.Where(e => e.EnquiryDate <= toDate.Value);
            }

            if (customerId.HasValue)
            {
                dbQuery = dbQuery.Where(e => e.CustomerId == customerId.Value);
            }

            if (!string.IsNullOrEmpty(query))
            {
                var q = query.ToLower();
                dbQuery = dbQuery.Where(e => e.EnquiryNumber.ToLower().Contains(q) || 
                                             (e.Customer != null && e.Customer.Name.ToLower().Contains(q)));
            }

            var enquiries = await dbQuery
                .OrderByDescending(e => e.EnquiryDate)
                .ToListAsync();

            // Project to match format shown in table grid (item count, customer details)
            return enquiries.Select(e => new
            {
                e.Id,
                e.EnquiryNumber,
                EnquiryDate = e.EnquiryDate.ToString("dd/MMM/yyyy"),
                Customer = e.Customer?.Name ?? string.Empty,
                Agent = e.Agent?.Name ?? "—",
                e.LeadType,
                City = e.Customer?.City ?? string.Empty,
                State = e.Customer?.State ?? string.Empty,
                e.Status,
                ItemsCount = e.EnquiryProducts.Count,
                e.Aging
            }).ToList<object>();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<SalesEnquiry>> GetEnquiry(int id)
        {
            var enquiry = await _context.SalesEnquiries
                .Include(e => e.Customer)
                .Include(e => e.Agent)
                .Include(e => e.AssignedAgent)
                .Include(e => e.EnquiryProducts)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (enquiry == null)
            {
                return NotFound();
            }

            return enquiry;
        }

        [HttpPost]
        public async Task<ActionResult<SalesEnquiry>> CreateEnquiry(SalesEnquiry enquiry)
        {
            // Auto-generate Enquiry Number (e.g. ENQ072600124)
            var count = await _context.SalesEnquiries.CountAsync() + 1;
            enquiry.EnquiryNumber = $"ENQ{DateTime.Now:MMddyy}{count:D5}";
            enquiry.EnquiryDate = DateTime.Now;
            enquiry.Aging = 0;
            enquiry.Status = "Pending";

            _context.SalesEnquiries.Add(enquiry);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetEnquiry), new { id = enquiry.Id }, enquiry);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateEnquiry(int id, SalesEnquiry enquiry)
        {
            if (id != enquiry.Id)
            {
                return BadRequest();
            }

            var existing = await _context.SalesEnquiries
                .Include(e => e.EnquiryProducts)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            // Update scalar fields
            existing.CustomerId = enquiry.CustomerId;
            existing.AgentId = enquiry.AgentId;
            existing.Source = enquiry.Source;
            existing.LeadType = enquiry.LeadType;
            existing.Address = enquiry.Address;
            existing.AssignToId = enquiry.AssignToId;
            existing.ExpiryDate = enquiry.ExpiryDate;
            existing.CustomerCountry = enquiry.CustomerCountry;
            existing.Remarks = enquiry.Remarks;
            existing.Status = enquiry.Status;

            // Simple replace of products
            _context.EnquiryProducts.RemoveRange(existing.EnquiryProducts);
            existing.EnquiryProducts = enquiry.EnquiryProducts;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!EnquiryExists(id))
                {
                    return NotFound();
                }
                else
                {
                    throw;
                }
            }

            return NoContent();
        }

        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteEnquiry(int id)
        {
            var enquiry = await _context.SalesEnquiries.FindAsync(id);
            if (enquiry == null)
            {
                return NotFound();
            }

            _context.SalesEnquiries.Remove(enquiry);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool EnquiryExists(int id)
        {
            return _context.SalesEnquiries.Any(e => e.Id == id);
        }
    }
}
