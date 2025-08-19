using BookCatAPI.Data;
using BookCatAPI.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace BookCatAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ArticleController : ControllerBase
    {
        private readonly BookCatDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IWebHostEnvironment _env;
        private readonly string _uploadPath;
        private readonly string _articlesPath;

        public ArticleController(BookCatDbContext context, IConfiguration configuration, IWebHostEnvironment env)
        {
            _context = context;
            _configuration = configuration;
            _env = env;
            _uploadPath = _configuration.GetValue<string>("AppConfig:UploadPath");
            _articlesPath = Path.Combine(_uploadPath, "articles");
            Directory.CreateDirectory(_articlesPath);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllArticles()
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var articles = await _context.Articles
                .OrderByDescending(a => a.CreatedAt)
                .Select(a => new Article
                {
                    Id = a.Id,
                    Title = a.Title,
                    Category = a.Category,
                    Slug = a.Slug,
                    Content = a.Content,
                    CreatedAt = a.CreatedAt,
                    UpdatedAt = a.UpdatedAt,
                    CoverImage = a.CoverImage == null ? null : $"{Request.Scheme}://{Request.Host}/library-uploads/articles/{a.CoverImage}"
                })
                .ToListAsync();

            return Ok(articles);
        }

        [HttpGet("by-id/{id:int}")] 
        public async Task<IActionResult> GetArticleById(int id)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var article = await _context.Articles.FindAsync(id);
            if (article == null) return NotFound();

            if (!string.IsNullOrEmpty(article.CoverImage))
            {
                article.CoverImage = $"{Request.Scheme}://{Request.Host}/library-uploads/articles/{article.CoverImage}";
            }

            return Ok(article);
        }

        [HttpGet("{slug}")]
        public async Task<IActionResult> GetArticleBySlug(string slug)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var article = await _context.Articles.FirstOrDefaultAsync(a => a.Slug == slug);
            if (article == null)
            {
                return NotFound();
            }

            if (!string.IsNullOrEmpty(article.CoverImage))
            {
                article.CoverImage = $"{Request.Scheme}://{Request.Host}/library-uploads/articles/{article.CoverImage}";
            }

            return Ok(article);
        }

        public class CreateOrUpdateArticleDto
        {
            public string Title { get; set; }
            public IFormFile? CoverImageFile { get; set; } 
            public string? Category { get; set; }
            public string Slug { get; set; }
            public string Content { get; set; }
        }

        [Authorize]
        [HttpPost]
        public async Task<IActionResult> CreateArticle([FromForm] CreateOrUpdateArticleDto dto)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var article = new Article
            {
                Title = dto.Title,
                Category = dto.Category,
                Slug = dto.Slug,
                Content = dto.Content,
                CreatedAt = DateTime.Now,
                UpdatedAt = DateTime.Now
            };

            if (dto.CoverImageFile != null && dto.CoverImageFile.Length > 0)
            {
                if (dto.CoverImageFile.Length > 5 * 1024 * 1024)
                {
                    return BadRequest("Розмір файлу не повинен перевищувати 5 МБ.");
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(dto.CoverImageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest("Дозволені лише файли форматів JPG, JPEG, PNG, GIF.");
                }

                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(_articlesPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.CoverImageFile.CopyToAsync(stream);
                }

                article.CoverImage = fileName; 
            }

            _context.Articles.Add(article);
            await _context.SaveChangesAsync();

            return CreatedAtAction(nameof(GetArticleById), new { id = article.Id }, article);
        }

        [Authorize]
        [HttpPut("{id}")]
        public async Task<IActionResult> UpdateArticle(int id, [FromForm] CreateOrUpdateArticleDto dto)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var article = await _context.Articles.FindAsync(id);
            if (article == null)
            {
                return NotFound();
            }

            article.Title = dto.Title;
            article.Category = dto.Category;
            article.Slug = dto.Slug;
            article.Content = dto.Content;
            article.UpdatedAt = DateTime.Now;

            if (dto.CoverImageFile != null && dto.CoverImageFile.Length > 0)
            {
                if (dto.CoverImageFile.Length > 5 * 1024 * 1024)
                {
                    return BadRequest("Розмір файлу не повинен перевищувати 5 МБ.");
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var fileExtension = Path.GetExtension(dto.CoverImageFile.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest("Дозволені лише файли форматів JPG, JPEG, PNG, GIF.");
                }

                if (!string.IsNullOrEmpty(article.CoverImage))
                {
                    var oldFilePath = Path.Combine(_articlesPath, article.CoverImage);
                    if (System.IO.File.Exists(oldFilePath))
                    {
                        System.IO.File.Delete(oldFilePath);
                    }
                }
             
                var fileName = $"{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(_articlesPath, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await dto.CoverImageFile.CopyToAsync(stream);
                }

                article.CoverImage = fileName; 
            }

            _context.Articles.Update(article);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        [Authorize]
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteArticle(int id)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var article = await _context.Articles.FindAsync(id);
            if (article == null) return NotFound();

            if (!string.IsNullOrEmpty(article.CoverImage))
            {
                var filePath = Path.Combine("wwwroot", article.CoverImage.TrimStart('/'));
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }

            _context.Articles.Remove(article);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}



