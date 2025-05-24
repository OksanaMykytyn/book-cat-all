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
        public BookController(BookCatDbContext context, ILogger<BookController> logger)
        {
            _context = context;
            _logger = logger; 
        }

        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateBook([FromBody] BookCreateDto bookDto)
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
                {
                    return Unauthorized("Невірне джерело запиту.");
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                var userLogin = User.FindFirst(ClaimTypes.Name)?.Value;

                if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(userLogin))
                {
                    _logger.LogWarning("Користувач неавторизований або токен недійсний.");
                    return Unauthorized("Користувач не авторизований.");
                }

                var library = await _context.Libraries.FirstOrDefaultAsync(l => l.UserId.ToString() == userId);
                if (library == null)
                {
                    _logger.LogError($"Бібліотека не знайдена для користувача з ID: {userId} / логіном: {userLogin}");
                    return BadRequest("Бібліотека не знайдена.");
                }

                if (string.IsNullOrWhiteSpace(bookDto.Name))
                {
                    return BadRequest("Поле 'Name' є обов'язковим.");
                }

                string inventoryNumber = bookDto.InventoryNumber;
                if (string.IsNullOrEmpty(inventoryNumber) || inventoryNumber == "0")
                {
                    inventoryNumber = library.StartInventory.ToString();
                    library.StartInventory += 1;
                    _context.Libraries.Update(library);
                }

                var book = new Book
                {
                    InventoryNumber = inventoryNumber,
                    Name = bookDto.Name,
                    Author = bookDto.Author,
                    Udk = bookDto.Udk,
                    UdkFormDocument = bookDto.UdkFormDocument,
                    Price = bookDto.Price,
                    CheckDocument = bookDto.CheckDocument,
                    YearPublishing = bookDto.YearPublishing,
                    Removed = bookDto.Removed,
                    LibraryId = library.Id
                };

                _context.Books.Add(book);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Книга '{book.Name}' успішно створена (ID: {book.Id}, Користувач: {userLogin})");

                return Ok(new
                {
                    book.Id,
                    book.Name,
                    book.InventoryNumber,
                    Message = "Книга успішно створена"
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при створенні книги.");
                return StatusCode(500, "Сталася внутрішня помилка сервера.");
            }
        }

        [Authorize]
        [HttpGet("list")]
        public async Task<IActionResult> GetBooks([FromQuery] int page = 1, [FromQuery] int limit = 20,
        [FromQuery] string? title = null,
        [FromQuery] string? author = null,
        [FromQuery] string? year = null,
        [FromQuery] string? udc = null,
        [FromQuery] string? udcForm = null,
        [FromQuery] string? accompanyingDoc = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Користувач не авторизований.");
            }

            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.UserId.ToString() == userId);
            if (library == null)
            {
                return BadRequest("Бібліотека не знайдена.");
            }

            var query = _context.Books
                .Where(b => b.LibraryId == library.Id && b.Removed == null)
                .AsQueryable();

            if (!string.IsNullOrEmpty(title))
                query = query.Where(b => b.Name.Contains(title));
            if (!string.IsNullOrEmpty(author))
                query = query.Where(b => b.Author.Contains(author));
            if (!string.IsNullOrEmpty(year))
                query = query.Where(b => b.YearPublishing.ToString() == year);
            if (!string.IsNullOrEmpty(udc))
                query = query.Where(b => b.Udk.Contains(udc));
            if (!string.IsNullOrEmpty(udcForm))
                query = query.Where(b => b.UdkFormDocument.Contains(udcForm));
            if (!string.IsNullOrEmpty(accompanyingDoc))
                query = query.Where(b => b.CheckDocument.Contains(accompanyingDoc));

            int totalBooks = await query.CountAsync();
            decimal totalPrice = await query
                .Where(b => b.Price.HasValue)
                .SumAsync(b => b.Price.Value);

            var books = await query
                .OrderByDescending(b => b.CreateAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            int totalPages = (int)Math.Ceiling(totalBooks / (double)limit);

            return Ok(new
            {
                books,
                totalPages,
                totalBooks,
                totalPrice
            });
        }



        [Authorize]
        [HttpPut("remove/{inventoryNumber}")]
        public async Task<IActionResult> RemoveBook(string inventoryNumber)
        {
            try
            {
                var book = await _context.Books.FirstOrDefaultAsync(b => b.InventoryNumber == inventoryNumber);

                if (book == null)
                {
                    return NotFound("Книгу не знайдено за інвентарним номером.");
                }

                if (book.Removed != null)
                {
                    return BadRequest("Книга вже була списана.");
                }

                book.Removed = DateTime.Now;
                _context.Books.Update(book);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Книга з інвентарним номером {inventoryNumber} була успішно списана.");

                return Ok(new
                {
                    Message = "Книгу успішно списано.",
                    RemovedAt = book.Removed
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при списанні книги.");
                return StatusCode(500, "Сталася внутрішня помилка сервера.");
            }
        }

        [Authorize]
        [HttpPut("unremove/{inventoryNumber}")]
        public async Task<IActionResult> UnRemoveBook(string inventoryNumber)
        {
            try
            {
                var book = await _context.Books.FirstOrDefaultAsync(b => b.InventoryNumber == inventoryNumber);

                if (book == null)
                {
                    return NotFound("Книгу не знайдено за інвентарним номером.");
                }

                if (book.Removed == null)
                {
                    return BadRequest("Книга не списана.");
                }

                book.Removed = null;
                _context.Books.Update(book);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Книга з інвентарним номером {inventoryNumber} була успішно повернена зі списання.");

                return Ok(new
                {
                    Message = "Книгу успішно повернено.",
                    RemovedAt = book.Removed
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при списанні книги.");
                return StatusCode(500, "Сталася внутрішня помилка сервера.");
            }
        }

        [Authorize]
        [HttpGet("list-removed")]
        public async Task<IActionResult> GetBooksRemoved([FromQuery] int page = 1, [FromQuery] int limit = 20, 
            [FromQuery] string? title = null,
            [FromQuery] string? author = null,
            [FromQuery] string? year = null,
            [FromQuery] string? udc = null,
            [FromQuery] string? udcForm = null,
            [FromQuery] string? accompanyingDoc = null,
            [FromQuery] DateTime? removed = null)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized("Користувач не авторизований.");
            }

            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.UserId.ToString() == userId);
            if (library == null)
            {
                return BadRequest("Бібліотека не знайдена.");
            }

            var query = _context.Books
                .Where(b => b.LibraryId == library.Id && b.Removed != null)
                .AsQueryable();

            if (!string.IsNullOrEmpty(title))
                query = query.Where(b => b.Name.Contains(title));
            if (!string.IsNullOrEmpty(author))
                query = query.Where(b => b.Author.Contains(author));
            if (!string.IsNullOrEmpty(year))
                query = query.Where(b => b.YearPublishing.ToString() == year);
            if (!string.IsNullOrEmpty(udc))
                query = query.Where(b => b.Udk.Contains(udc));
            if (!string.IsNullOrEmpty(udcForm))
                query = query.Where(b => b.UdkFormDocument.Contains(udcForm));
            if (!string.IsNullOrEmpty(accompanyingDoc))
                query = query.Where(b => b.CheckDocument.Contains(accompanyingDoc));
            if (removed.HasValue)
            {
                var date = removed.Value.Date;
                query = query.Where(b => b.Removed.HasValue && b.Removed.Value.Date == date);
            }

            int totalBooks = await query.CountAsync();
            decimal totalPrice = await query
                .Where(b => b.Price.HasValue)
                .SumAsync(b => b.Price.Value);

            var books = await query
                .OrderByDescending(b => b.CreateAt)
                .Skip((page - 1) * limit)
                .Take(limit)
                .ToListAsync();

            int totalPages = (int)Math.Ceiling(totalBooks / (double)limit);

            return Ok(new
            {
                books,
                totalPages,
                totalBooks,
                totalPrice
            });
        }

        [Authorize]
        [HttpPut("update/{inventoryNumber}")]
        public async Task<IActionResult> UpdateBook(string inventoryNumber, [FromBody] UpdateBookDto updatedBook)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var book = await _context.Books.FirstOrDefaultAsync(b => b.InventoryNumber == inventoryNumber);

            if (book == null)
                return NotFound($"Книга з інвентарним номером {inventoryNumber} не знайдена.");

            book.InventoryNumber = updatedBook.InventoryNumber;
            book.Name = updatedBook.Name;
            book.Author = updatedBook.Author;
            book.YearPublishing = updatedBook.YearPublishing;
            book.Udk = updatedBook.Udk;
            book.UdkFormDocument = updatedBook.UdkFormDocument;
            book.CheckDocument = updatedBook.CheckDocument;
            book.Price = updatedBook.Price;
            book.Removed = updatedBook.Removed;

            await _context.SaveChangesAsync();

            return Ok(book);
        }

        [Authorize]
        [HttpGet("get/{inventoryNumber}")]
        public async Task<IActionResult> GetBookByInventoryNumber(string inventoryNumber)
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
                {
                    return Unauthorized("Невірне джерело запиту.");
                }

                var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
                if (string.IsNullOrEmpty(userId))
                {
                    return Unauthorized("Користувач не авторизований.");
                }

                var library = await _context.Libraries.FirstOrDefaultAsync(l => l.UserId.ToString() == userId);
                if (library == null)
                {
                    return BadRequest("Бібліотека не знайдена.");
                }

                var book = await _context.Books.FirstOrDefaultAsync(b => b.InventoryNumber == inventoryNumber && b.LibraryId == library.Id);
                if (book == null)
                {
                    return NotFound("Книгу з таким інвентарним номером не знайдено.");
                }

                return Ok(book);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Помилка при спробі отримати книгу з інвентарним номером {inventoryNumber}.");
                return StatusCode(500, "Сталася внутрішня помилка сервера.");
            }
        }

        [Authorize]
        [HttpDelete("delete/{inventoryNumber}")]
        public async Task<IActionResult> DeleteBook(string inventoryNumber)
        {
            try
            {
                if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
                {
                    return Unauthorized("Невірне джерело запиту.");
                }

                var book = await _context.Books.FirstOrDefaultAsync(b => b.InventoryNumber == inventoryNumber);

                if (book == null)
                {
                    return NotFound($"Книга з інвентарним номером {inventoryNumber} не знайдена.");
                }

                _context.Books.Remove(book);
                await _context.SaveChangesAsync();

                _logger.LogInformation($"Книга з інвентарним номером {inventoryNumber} була успішно видалена.");

                return Ok(new
                {
                    Message = "Книгу успішно видалено з бази даних.",
                    BookName = book.Name,
                    InventoryNumber = book.InventoryNumber
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Помилка при видаленні книги.");
                return StatusCode(500, "Сталася внутрішня помилка сервера.");
            }
        }


    }
}
