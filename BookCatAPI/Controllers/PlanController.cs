using BookCatAPI.Data;
using BookCatAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BookCatAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PlanController : ControllerBase
    {
        private readonly BookCatDbContext _context;
        public PlanController(BookCatDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetPlans()
        {
            var plans = await _context.Plans.OrderBy(p => p.Id).ToListAsync();
            return Ok(plans);
        }

        public class UpdatePlanDto
        {
            public int MaxBooks { get; set; }
            public decimal Price { get; set; }
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdatePlan(int id, [FromBody] UpdatePlanDto updated)
        {
            var plan = await _context.Plans.FindAsync(id);
            if (plan is null) return NotFound();

            plan.MaxBooks = updated.MaxBooks;
            plan.Price = updated.Price;

            await _context.SaveChangesAsync();
            return NoContent();
        }

    }
}
