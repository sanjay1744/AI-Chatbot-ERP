using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AriyAI.ERP.Api.Data;
using AriyAI.ERP.Api.Models;

namespace AriyAI.ERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class QuotationsController : ControllerBase
    {
        private readonly ErpDbContext _context;

        public QuotationsController(ErpDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<IEnumerable<object>>> GetQuotations(
            [FromQuery] string? status = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] int? customerId = null,
            [FromQuery] string? query = null)
        {
            var dbQuery = _context.Quotations
                .Include(q => q.Customer)
                .Include(q => q.Agent)
                .AsQueryable();

            if (!string.IsNullOrEmpty(status) && !status.Equals("All", StringComparison.OrdinalIgnoreCase))
            {
                dbQuery = dbQuery.Where(q => q.Status == status);
            }

            if (fromDate.HasValue)
            {
                dbQuery = dbQuery.Where(q => q.QuotationDate >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                dbQuery = dbQuery.Where(q => q.QuotationDate <= toDate.Value);
            }

            if (customerId.HasValue)
            {
                dbQuery = dbQuery.Where(q => q.CustomerId == customerId.Value);
            }

            if (!string.IsNullOrEmpty(query))
            {
                var q = query.ToLower();
                dbQuery = dbQuery.Where(qB => qB.QuotationNumber.ToLower().Contains(q) || 
                                             (qB.Customer != null && qB.Customer.Name.ToLower().Contains(q)));
            }

            var quotations = await dbQuery
                .OrderByDescending(q => q.QuotationDate)
                .ToListAsync();

            return quotations.Select(q => new
            {
                q.Id,
                q.QuotationNumber,
                QuotationDate = q.QuotationDate.ToString("dd-MMM-yyyy"),
                CustomerName = q.Customer?.Name ?? string.Empty,
                Agent = q.Agent?.Name ?? "—",
                State = q.Customer?.State ?? string.Empty,
                ItemsCount = q.QuotationProducts.Count,
                q.Aging,
                q.Status
            }).ToList<object>();
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<Quotation>> GetQuotation(int id)
        {
            var quotation = await _context.Quotations
                .Include(q => q.Customer)
                .Include(q => q.Agent)
                .Include(q => q.QuotationProducts)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (quotation == null)
            {
                return NotFound();
            }

            return quotation;
        }

        [HttpPost]
        public async Task<ActionResult<Quotation>> CreateQuotation(Quotation quotation)
        {
            // Auto-generate Quotation Number (e.g. QU0072600279)
            var count = await _context.Quotations.CountAsync() + 1;
            quotation.QuotationNumber = $"QU00{DateTime.Now:Mddyy}{count:D4}";
            quotation.QuotationDate = DateTime.Now;
            quotation.Aging = 0;
            quotation.Status = "Pending";

            _context.Quotations.Add(quotation);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetQuotation), new { id = quotation.Id }, quotation);
        }

        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateQuotation(int id, Quotation quotation)
        {
            if (id != quotation.Id)
            {
                return BadRequest();
            }

            var existing = await _context.Quotations
                .Include(q => q.QuotationProducts)
                .FirstOrDefaultAsync(q => q.Id == id);

            if (existing == null)
            {
                return NotFound();
            }

            existing.CustomerReference = quotation.CustomerReference;
            existing.Currency = quotation.Currency;
            existing.DueDate = quotation.DueDate;
            existing.CustomerId = quotation.CustomerId;
            existing.Address = quotation.Address;
            existing.AgentId = quotation.AgentId;
            existing.Subject1 = quotation.Subject1;
            existing.Subject2 = quotation.Subject2;
            existing.Status = quotation.Status;

            _context.QuotationProducts.RemoveRange(existing.QuotationProducts);
            existing.QuotationProducts = quotation.QuotationProducts;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                if (!QuotationExists(id))
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
        public async Task<IActionResult> DeleteQuotation(int id)
        {
            var quotation = await _context.Quotations.FindAsync(id);
            if (quotation == null)
            {
                return NotFound();
            }

            _context.Quotations.Remove(quotation);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        private bool QuotationExists(int id)
        {
            return _context.Quotations.Any(q => q.Id == id);
        }
    }
}
