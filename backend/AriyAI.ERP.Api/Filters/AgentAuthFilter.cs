using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.EntityFrameworkCore;
using AriyAI.ERP.Api.Data;

namespace AriyAI.ERP.Api.Filters
{
    public class AgentAuthFilter : IAsyncActionFilter
    {
        private readonly ErpDbContext _db;

        public AgentAuthFilter(ErpDbContext db)
        {
            _db = db;
        }

        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {
            var authHeader = context.HttpContext.Request.Headers["Authorization"].ToString();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                context.Result = new UnauthorizedObjectResult(new { detail = "Authorization token is missing." });
                return;
            }

            var token = authHeader.Substring("Bearer ".Length).Trim();
            if (string.IsNullOrEmpty(token))
            {
                context.Result = new UnauthorizedObjectResult(new { detail = "Authorization token is empty." });
                return;
            }

            var agent = await _db.Agents
                .FirstOrDefaultAsync(a => a.SessionToken == token);

            if (agent == null)
            {
                context.Result = new UnauthorizedObjectResult(new { detail = "Invalid or expired session token." });
                return;
            }

            if (agent.TokenExpiresAt != null && agent.TokenExpiresAt < DateTime.UtcNow)
            {
                context.Result = new UnauthorizedObjectResult(new { detail = "Session has expired. Please login again." });
                return;
            }

            // Expose the logged-in agent to the current request scope
            context.HttpContext.Items["CurrentAgent"] = agent;

            await next();
        }
    }
}
