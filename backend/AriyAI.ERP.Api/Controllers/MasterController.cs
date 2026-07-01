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
    }
}
