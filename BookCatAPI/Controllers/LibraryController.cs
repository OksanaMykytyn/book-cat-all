using BookCatAPI.Data;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using BookCatAPI.Models.DTOs;
using Microsoft.Extensions.Configuration;

namespace BookCatAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class LibraryController : ControllerBase
    {
        private readonly BookCatDbContext _context;
        private readonly ILogger<LibraryController> _logger;
        private readonly IConfiguration _configuration;
        private readonly string _uploadPath;

        public LibraryController(BookCatDbContext context, ILogger<LibraryController> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
            _uploadPath = _configuration.GetValue<string>("AppConfig:UploadPath");
        }

        [Authorize]
        [HttpGet("status")]
        public async Task<IActionResult> GetLibraryStatus()
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var userLogin = User.FindFirst(ClaimTypes.Name)?.Value;

            if (userLogin != null && userLogin.ToLower() == "bookcatalog.library@gmail.com")
            {
                return Ok(new { status = "admin" });
            }

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
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

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
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

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
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

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
                    : $"{Request.Scheme}://{Request.Host}/library-uploads/" + library.User.Userimage.Replace("\\", "/"),
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
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

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

            if (library.PlanId != dto.PlanId)
            {
                library.PlanId = dto.PlanId;
                library.Status = "pending";
                library.DataEndPlan = DateTime.Today;
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "Текстові дані оновлено." });
        }

        [Authorize]
        [HttpPut("setup/image")]
        public async Task<IActionResult> UpdateLibraryImage([FromForm] IFormFile image)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

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
                var newFileName = Guid.NewGuid().ToString() + Path.GetExtension(image.FileName); 

                var relativePath = Path.Combine(library.Id.ToString(), "images");
                var fullFolderPath = Path.Combine(_uploadPath, relativePath);

                Directory.CreateDirectory(fullFolderPath);

                var fullFilePath = Path.Combine(fullFolderPath, newFileName);
                using var stream = new FileStream(fullFilePath, FileMode.Create);
                await image.CopyToAsync(stream);

                var relativeFilePath = Path.Combine(relativePath, newFileName);
                library.User.Userimage = relativeFilePath.Replace("\\", "/"); 

                await _context.SaveChangesAsync();

                var imageUrl = $"{Request.Scheme}://{Request.Host}/library-uploads/" + library.User.Userimage;

                return Ok(new
                {
                    message = "Зображення оновлено.",
                    imageUrl = imageUrl
                });
            }

            return BadRequest("Файл зображення не завантажено.");
        }

}
}
