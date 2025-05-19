using BookCatAPI.Data;
using BookCatAPI.Models;
using BookCatAPI.Models.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.Extensions.Logging;

namespace BookCatAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BookController : ControllerBase
    {
        private readonly BookCatDbContext _context;
        private readonly ILogger<BookController> _logger;
        public BookController(BookCatDbContext context, ILogger<BookController> logger) // Інжектуйте ILogger
        {
            _context = context;
            _logger = logger; // Присвойте логер
        }

        [Authorize] // Доступ лише для авторизованих користувачів
        [HttpPost("create")]
        public async Task<IActionResult> CreateBook([FromBody] BookCreateDto bookDto)
        {
            // Отримання ID користувача з токена
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId == null)
            {
                _logger.LogWarning("Unauthorized access attempt: User ID not found in token."); // Логування попередження
                return Unauthorized("Користувач не авторизований.");
            }
            // Знайти бібліотеку за ID користувача
            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.UserId.ToString() == userId);
            if (library == null)
            {
                _logger.LogError($"Library not found for user ID: {userId}"); // Логування помилки
                return BadRequest("Бібліотека не знайдена.");
            }

            // Якщо інвентарний номер не надано або вказано як "0", отримати start_inventory з бібліотеки
            if (string.IsNullOrEmpty(bookDto.InventoryNumber) || bookDto.InventoryNumber == "0")
            {
                bookDto.InventoryNumber = library.StartInventory.ToString();
                library.StartInventory += 1; // Збільшити start_inventory на 1
                _context.Libraries.Update(library); // Оновити бібліотеку
            }
            // Створення нової книги
            var book = new Book
            {
                InventoryNumber = bookDto.InventoryNumber,
                Name = bookDto.Name,
                Author = bookDto.Author,
                Udk = bookDto.Udk,
                UdkFormDocument = bookDto.UdkFormDocument,
                Price = bookDto.Price,
                CheckDocument = bookDto.CheckDocument,
                YearPublishing = bookDto.YearPublishing,
                LibraryId = library.Id, // Використовуємо ID бібліотеки
                Removed = bookDto.Removed // Значення за замовчуванням - null
            };
            _context.Books.Add(book);
            await _context.SaveChangesAsync();
            _logger.LogInformation($"Book created successfully: {book.Name} (ID: {book.Id})"); // Логування інформації
            return Ok(new { book.Id, book.Name, book.InventoryNumber });
        }
    }
}
