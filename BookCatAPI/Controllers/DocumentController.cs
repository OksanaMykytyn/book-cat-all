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
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

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
                    .OrderBy(b => b.InventoryNumber) 
                    .ToListAsync();

                await GenerateWriteOffAct(fullFilePath, books);
            }
            else if (dto.Format == "inventoryBook")
            {
                books = await _context.Books
                    .Where(b => b.Removed == null && b.LibraryId == library.Id)
                    .ToListAsync();

                await GenerateInventoryBook(fullFilePath, books, dto.Name);
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

        private async Task GenerateWriteOffAct(string outputPath, List<Book> books)
        {
            string templatePath = Path.Combine(_env.WebRootPath, "Templates", "writeOffAct.docx");

            if (!System.IO.File.Exists(templatePath))
            {
                _logger.LogError($"Шаблон акту списання не знайдено за шляхом: {templatePath}");
                throw new FileNotFoundException("Шаблон акту списання не знайдено.");
            }

            System.IO.File.Copy(templatePath, outputPath, true);

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(outputPath, true))
            {
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                if (body == null)
                {
                    _logger.LogError("Не вдалося отримати тіло документа.");
                    return;
                }

                Table targetTable = body.Elements<Table>().FirstOrDefault();
                if (targetTable == null)
                {
                    _logger.LogWarning("Таблиця в шаблоні акту списання не знайдена.");
                    return;
                }

                books = books.OrderBy(b =>
                {
                    if (int.TryParse(b.InventoryNumber, out int invNumber))
                        return invNumber;
                    return int.MaxValue; 
                }).ToList();

                int rowCount = 0;
                decimal totalPrice = 0;

                foreach (var book in books)
                {
                    rowCount++;
                    decimal price = book.Price ?? 0;
                    totalPrice += price;

                    var newRow = new TableRow(); 

                    string[] priceParts = price.ToString("F2").Split(',');
                    if (priceParts.Length < 2)
                        priceParts = price.ToString("F2").Split('.');

                    string whole = priceParts[0];
                    string frac = priceParts.Length > 1 ? priceParts[1] : "00";

                    newRow.Append(
                        CreateTableCell(rowCount.ToString(), align: JustificationValues.Right),                    
                        CreateTableCell(book.InventoryNumber ?? "", align: JustificationValues.Right),            
                        CreateTableCell($"{book.Author ?? ""} {book.Name ?? ""}", align: JustificationValues.Left), 
                        CreateTableCell("1", align: JustificationValues.Center),                                   
                        CreateTableCell($"{whole},{frac}", align: JustificationValues.Right),                      
                        CreateTableCell(whole, align: JustificationValues.Right),                                  
                        CreateTableCell(frac, align: JustificationValues.Right),                                   
                        CreateTableCell(book.YearPublishing?.ToString() ?? "", align: JustificationValues.Center)  
                    );


                    targetTable.AppendChild(newRow);
                }

                var totalParagraph = body.Elements<Paragraph>()
                    .FirstOrDefault(p => p.InnerText.Contains("Всього на суму "));

                if (totalParagraph != null)
                {
                    var run = totalParagraph.Elements<Run>().LastOrDefault();
                    if (run != null)
                    {
                        var textElement = run.Elements<Text>().LastOrDefault();
                        if (textElement != null)
                        {
                            textElement.Text = $"\nВсього на суму: \t{totalPrice.ToString("F2")} грн.";
                        }
                    }
                }

                wordDoc.MainDocumentPart.Document.Save();
            }
        }



        private static TableCell CreateTableCell(string text, JustificationValues align, bool bold = false)
        {
            var runProps = new RunProperties(
                new RunFonts
                {
                    Ascii = "Times New Roman",
                    HighAnsi = "Times New Roman",
                    EastAsia = "Times New Roman",
                    ComplexScript = "Times New Roman"
                },
                new FontSize() { Val = "20" }
            );

            if (bold)
                runProps.Append(new Bold());

            var run = new Run();
            run.Append(runProps);
            run.Append(new Text(text) { Space = SpaceProcessingModeValues.Preserve });

            var paragraph = new Paragraph(
                new ParagraphProperties(new Justification() { Val = align }),
                run
            );

            return new TableCell(paragraph);
        }

        private async Task GenerateInventoryBook(string outputPath, List<Book> books, string documentTitle)
        {
            string templatePath = Path.Combine(_env.WebRootPath, "Templates", "InventoryBook.docx");

            if (!System.IO.File.Exists(templatePath))
            {
                _logger.LogError($"Шаблон інвентарної книги не знайдено за шляхом: {templatePath}");
                throw new FileNotFoundException("Шаблон інвентарної книги не знайдено.");
            }

            System.IO.File.Copy(templatePath, outputPath, true);

            using (WordprocessingDocument wordDoc = WordprocessingDocument.Open(outputPath, true))
            {
                var body = wordDoc.MainDocumentPart?.Document?.Body;
                if (body == null)
                {
                    _logger.LogError("Не вдалося отримати тіло документа.");
                    return;
                }

                Table targetTable = body.Elements<Table>().FirstOrDefault();
                if (targetTable == null)
                {
                    _logger.LogWarning("Таблиця в шаблоні інвентарної книги не знайдена.");
                    return;
                }

                books = books.OrderBy(b =>
                {
                    if (int.TryParse(b.InventoryNumber, out int invNum))
                        return invNum;
                    return int.MaxValue;
                }).ToList();

                foreach (var book in books)
                {
                    decimal price = book.Price ?? 0;

                    string[] priceParts = price.ToString("F2").Split(',');
                    if (priceParts.Length < 2)
                        priceParts = price.ToString("F2").Split('.');

                    string whole = priceParts[0];
                    string frac = priceParts.Length > 1 ? priceParts[1] : "00";

                    var newRow = new TableRow();
                    newRow.Append(
                        CreateTableCell(book.InventoryNumber ?? "", JustificationValues.Left),           
                        CreateTableCell(book.Author ?? "", JustificationValues.Left),                    
                        CreateTableCell(book.Name ?? "", JustificationValues.Left),                      
                        CreateTableCell("1", JustificationValues.Center),                           
                        CreateTableCell(whole, JustificationValues.Right),                               
                        CreateTableCell(frac, JustificationValues.Right),                                
                        CreateTableCell(book.YearPublishing?.ToString() ?? "", JustificationValues.Center), 
                        CreateTableCell(book.CheckDocument ?? "", JustificationValues.Left),             
                        CreateTableCell(book.Udk ?? "", JustificationValues.Left),                      
                        CreateTableCell(book.UdkFormDocument ?? "", JustificationValues.Left)           
                    );
                    targetTable.AppendChild(newRow);
                }

                wordDoc.MainDocumentPart.Document.Save();
            }
        }

        [Authorize]
        [HttpGet("all")]
        public async Task<IActionResult> GetAllDocuments()
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