using BookCatAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;

namespace BookCatAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LibraryController : ControllerBase
    {
        private readonly BookCatDbContext _context;
        private readonly ILogger<LibraryController> _logger;

        public LibraryController(BookCatDbContext context, ILogger<LibraryController> logger)
        {
            _context = context;
            _logger = logger;
        }

        [HttpGet("status")]
        [Authorize]
        public async Task<IActionResult> GetLibraryStatus()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.UserId.ToString() == userId);

            if (library == null)
            {
                return NotFound("Бібліотека не знайдена.");
            }

            return Ok(new { status = library.Status });
        }

        public class InventoryUpdateDto
        {
            public int Inventory { get; set; }
        }

        [HttpGet("inventory")]
        [Authorize]
        public async Task<IActionResult> GetInventoryNumber()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Користувач не авторизований.");

            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.UserId.ToString() == userId);
            if (library == null)
                return NotFound("Бібліотека не знайдена.");

            return Ok(new { inventory = library.StartInventory });
        }

        [HttpPut("inventory")]
        [Authorize]
        public async Task<IActionResult> UpdateInventoryNumber([FromBody] InventoryUpdateDto dto)
        {
            if (dto.Inventory < 0)
                return BadRequest("Інвентарний номер не може бути менше 0.");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Користувач не авторизований.");

            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.UserId.ToString() == userId);
            if (library == null)
                return NotFound("Бібліотека не знайдена.");

            library.StartInventory = dto.Inventory;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Інвентарний номер оновлено успішно", inventory = library.StartInventory });
        }

    }
}
