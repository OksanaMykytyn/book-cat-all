using BookCatAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using BookCatAPI.Models.DTOs;

namespace BookCatAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LibraryController : ControllerBase
    {
        private readonly BookCatDbContext _context;
        private readonly ILogger<LibraryController> _logger;
        private readonly IWebHostEnvironment _env;
        
        public LibraryController(BookCatDbContext context, ILogger<LibraryController> logger, IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;

        }

        [Authorize]
        [HttpGet("status")]
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

        [Authorize]
        [HttpGet("inventory")]
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

        [Authorize]
        [HttpPut("inventory")]
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

        [Authorize]
        [HttpGet("setup")]
        public async Task<IActionResult> GetLibrarySetup()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Користувач не авторизований.");

            var library = await _context.Libraries
                .Include(l => l.User)
                .Include(l => l.Plan)
                .Include(l => l.Books)
                .FirstOrDefaultAsync(l => l.UserId.ToString() == userId);

            if (library == null)
                return NotFound("Бібліотека не знайдена.");

            var response = new
            {
                username = library.User.Username,
                login = library.User.Userlogin,
                image = string.IsNullOrEmpty(library.User.Userimage)
                    ? null
                    : "/" + library.User.Userimage.Replace("\\", "/"),
                booksCount = library.Books.Count(),
                planId = library.PlanId,
                maxBooks = library.Plan?.MaxBooks,
                planEndDate = library.DataEndPlan,
                status = library.Status
            };

            return Ok(response);
        }


        [Authorize]
        [HttpPut("setup/text")]
        public async Task<IActionResult> UpdateLibraryText([FromBody] LibraryUpdateDto dto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Користувач не авторизований.");

            var library = await _context.Libraries
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.UserId.ToString() == userId);

            if (library == null)
                return NotFound("Бібліотека не знайдена.");

            library.User.Username = dto.Username;
            library.User.Userlogin = dto.Email;
            library.PlanId = dto.PlanId;

            await _context.SaveChangesAsync();

            return Ok(new { message = "Текстові дані оновлено." });
        }



        [Authorize]
        [HttpPut("setup/image")]
        public async Task<IActionResult> UpdateLibraryImage([FromForm] IFormFile image)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Користувач не авторизований.");

            var library = await _context.Libraries
                .Include(l => l.User)
                .FirstOrDefaultAsync(l => l.UserId.ToString() == userId);

            if (library == null)
                return NotFound("Бібліотека не знайдена.");

            if (image != null && image.Length > 0)
            {
                var newFileName = Path.GetFileName(image.FileName);
                var currentFileName = Path.GetFileName(library.User.Userimage ?? "");

                if (!string.Equals(newFileName, currentFileName, StringComparison.OrdinalIgnoreCase))
                {
                    var dateFolder = DateTime.Now.ToString("yyyy-MM-dd");
                    var relativePath = Path.Combine("LibraryFiles", library.Id.ToString(), "images", dateFolder);
                    var fullFolderPath = Path.Combine(_env.WebRootPath!, relativePath);

                    Directory.CreateDirectory(fullFolderPath);

                    var fullFilePath = Path.Combine(fullFolderPath, newFileName);
                    using var stream = new FileStream(fullFilePath, FileMode.Create);
                    await image.CopyToAsync(stream);

                    var relativeFilePath = Path.Combine(relativePath, newFileName).Replace("\\", "/");
                    library.User.Userimage = relativeFilePath;
                }

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    message = "Зображення оновлено.",
                    imageUrl = "/" + library.User.Userimage.Replace("\\", "/")
                });
            }

            return BadRequest("Файл зображення не завантажено.");
        }




    }
}
