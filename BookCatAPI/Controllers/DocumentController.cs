using BookCatAPI.Data;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using DocumentFormat.OpenXml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using BookCatAPI.Models;
using System.Security.Claims;
using BookCatAPI.Models.DTOs;
using Document = DocumentFormat.OpenXml.Wordprocessing.Document;

namespace BookCatAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentController : ControllerBase
    {
        private readonly BookCatDbContext _context;
        private readonly ILogger<DocumentController> _logger;

        private readonly IWebHostEnvironment _env;

        public DocumentController(BookCatDbContext context, ILogger<DocumentController> logger, IWebHostEnvironment env)
        {
            _context = context;
            _logger = logger;
            _env = env;
        }


        [Authorize]
        [HttpPost("create")]
        public async Task<IActionResult> CreateDocument([FromBody] CreateDocumentDto dto)
        {
            if (string.IsNullOrEmpty(_env.WebRootPath))
            {
                return StatusCode(500, "WebRootPath не налаштований.");
            }


            if (string.IsNullOrWhiteSpace(dto.Name))
                return BadRequest("Назва документу не може бути порожньою.");

            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Користувач не авторизований.");

            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.UserId.ToString() == userId);
            if (library == null)
                return BadRequest("Бібліотека не знайдена.");

            var dateNow = DateTime.Now;
            string dateFolder = dateNow.ToString("yyyy-MM-dd");

            var fileName = $"{dto.Name}.docx";
            string relativePath = Path.Combine("LibraryFiles", library.Id.ToString(), dateFolder);
            string fullDirectoryPath = Path.Combine(_env.WebRootPath, relativePath);
            Directory.CreateDirectory(fullDirectoryPath);

            string fullFilePath = Path.Combine(fullDirectoryPath, fileName);
            string fileUrl = $"/{relativePath.Replace("\\", "/")}/{fileName}";

            List<Book> books;
            if (dto.Format == "writeOffAct" && dto.DateFrom != null && dto.DateTo != null)
            {
                DateTime from = dto.DateFrom.Value.Date;
                DateTime to = dto.DateTo.Value.Date.AddDays(1).AddTicks(-1);
                books = await _context.Books
                    .Where(b => b.Removed != null && b.Removed >= from && b.Removed <= to && b.LibraryId == library.Id)
                    .ToListAsync();
            }
            else
            {
                books = await _context.Books
                    .Where(b => b.Removed == null && b.LibraryId == library.Id)
                    .ToListAsync();
            }

            using (var fileStream = new FileStream(fullFilePath, FileMode.Create))
            using (var wordDoc = WordprocessingDocument.Create(fileStream, WordprocessingDocumentType.Document, true))
            {
                var mainPart = wordDoc.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                var heading = new Paragraph(new Run(new Text(dto.Name)))
                {
                    ParagraphProperties = new ParagraphProperties(new Justification() { Val = JustificationValues.Center })
                };
                body.AppendChild(heading);
                body.AppendChild(new Paragraph(new Run(new Text(""))));

                var table = new Table();

                var tblProps = new TableProperties(
                    new TableBorders(
                        new TopBorder { Val = BorderValues.Single, Size = 4 },
                        new BottomBorder { Val = BorderValues.Single, Size = 4 },
                        new LeftBorder { Val = BorderValues.Single, Size = 4 },
                        new RightBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4 },
                        new InsideVerticalBorder { Val = BorderValues.Single, Size = 4 }
                    )
                );
                table.AppendChild(tblProps);

                var headerRow = new TableRow();
                headerRow.Append(
                    CreateTableCell("Інвентарний номер"),
                    CreateTableCell("Назва"),
                    CreateTableCell("Автор"),
                    CreateTableCell("Рік видання")
                );
                table.AppendChild(headerRow);

                foreach (var book in books)
                {
                    var row = new TableRow();
                    row.Append(
                        CreateTableCell(book.InventoryNumber ?? ""),
                        CreateTableCell(book.Name ?? ""),
                        CreateTableCell(book.Author ?? ""),
                        CreateTableCell(book.YearPublishing?.ToString() ?? "")
                    );
                    table.AppendChild(row);
                }

                body.AppendChild(table);
                mainPart.Document.Save();
            }

            var document = new Models.Document
            {
                Name = dto.Name,
                Format = dto.Format == "writeOffAct" ? "removed" : "allbooks",
                Url = fileUrl,
                LibraryId = library.Id,
                CreateAt = dateNow,
                DateStart = dto.Format == "writeOffAct" ? dto.DateFrom : null,
                DateEnd = dto.Format == "writeOffAct" ? dto.DateTo : null
            };

            _context.Documents.Add(document);
            await _context.SaveChangesAsync();

            var fileBytes = await System.IO.File.ReadAllBytesAsync(fullFilePath);
            return File(fileBytes, "application/vnd.openxmlformats-officedocument.wordprocessingml.document", fileName);
        }

        private static TableCell CreateTableCell(string text, bool bold = false)
        {
            var runProperties = new RunProperties();
            if (bold)
            {
                runProperties.Append(new Bold());
            }

            var run = new Run();
            run.Append(runProperties);
            run.Append(new Text(text));

            var paragraph = new Paragraph(run);

            return new TableCell(paragraph);
        }

        [Authorize]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllDocuments()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId))
                return Unauthorized("Користувач не авторизований.");

            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.UserId.ToString() == userId);
            if (library == null)
                return BadRequest("Бібліотека не знайдена.");

            var documents = await _context.Documents
                .Where(d => d.LibraryId == library.Id)
                .OrderByDescending(d => d.CreateAt)
                .Select(d => new DocumentInfoDto
                {
                    Name = d.Name,
                    Url = d.Url,
                    CreateAt = d.CreateAt
                })
                .ToListAsync();

            return Ok(documents);
        }

    }
}
