using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AriyAI.ERP.Api.Data;
using AriyAI.ERP.Api.Models;

namespace AriyAI.ERP.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class MasterController : ControllerBase
    {
        private readonly ErpDbContext _context;

        public MasterController(ErpDbContext context)
        {
            _context = context;
        }

        [HttpGet("customers")]
        public async Task<ActionResult<IEnumerable<Customer>>> GetCustomers()
        {
            return await _context.Customers.OrderBy(c => c.Name).ToListAsync();
        }

        [HttpPost("customers")]
        public async Task<ActionResult<Customer>> CreateCustomer([FromBody] Customer customer)
        {
            if (customer == null || string.IsNullOrWhiteSpace(customer.Name))
            {
                return BadRequest("Customer name is required.");
            }

            _context.Customers.Add(customer);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetCustomers), new { id = customer.Id }, customer);
        }

        [HttpGet("agents")]
        public async Task<ActionResult<IEnumerable<Agent>>> GetAgents()
        {
            return await _context.Agents.OrderBy(a => a.Name).ToListAsync();
        }

        [HttpGet("products")]
        public async Task<ActionResult<IEnumerable<Product>>> GetProducts()
        {
            return await _context.Products.OrderBy(p => p.Description).ToListAsync();
        }

        [HttpGet("potential-items")]
        public async Task<ActionResult<IEnumerable<PotentialItem>>> GetPotentialItems()
        {
            return await _context.PotentialItems.OrderBy(p => p.Name).ToListAsync();
        }

        [HttpPost("potential-items")]
        public async Task<ActionResult<PotentialItem>> CreatePotentialItem([FromBody] PotentialItem item)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.Name))
            {
                return BadRequest("Item description/name is required.");
            }

            _context.PotentialItems.Add(item);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetPotentialItems), new { id = item.Id }, item);
        }
    }
}
