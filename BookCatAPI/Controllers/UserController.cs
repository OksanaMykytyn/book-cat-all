using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System;
using BookCatAPI.Models;
using Microsoft.EntityFrameworkCore;
using BookCatAPI.Models.DTOs;
using BookCatAPI.Data;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;


namespace BookCatAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly BookCatDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher = new PasswordHasher<User>();
        private readonly IConfiguration _configuration;


        public UserController(BookCatDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }
        
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] UserRegistrationDto userDto)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            if (await _context.Users.AnyAsync(u => u.Userlogin == userDto.Userlogin))
            {
                return BadRequest("Користувач з таким логіном вже існує.");
            }

            if (string.IsNullOrWhiteSpace(userDto.Username) || userDto.Username.Length > 100 ||
                !Regex.IsMatch(userDto.Username, @"^[А-Яа-яІіЇїЄєA-Za-z0-9\s]{1,100}$"))
            {
                return BadRequest("Недійсна назва школи/бібліотеки.");
            }

            if (!Regex.IsMatch(userDto.Userlogin, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            {
                return BadRequest("Невірний формат логіну (електронна пошта).");
            }

            if (!Regex.IsMatch(userDto.Userpassword, @"^[A-Za-z0-9]{8,20}$"))
            {
                return BadRequest("Пароль повинен містити 8–20 символів (англійські літери та цифри).");
            }


            var user = new User
            {
                Username = userDto.Username,
                Userlogin = userDto.Userlogin,
                Userpassword = _passwordHasher.HashPassword(null, userDto.Userpassword)

            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            var library = new Library
            {
                UserId = user.Id,
                PlanId = userDto.PlanId,
                Status = "pending"
            };
            _context.Libraries.Add(library);
            await _context.SaveChangesAsync();

            //HttpContext.Session.SetString("Username", user.Username);
            //HttpContext.Session.SetInt32("User Id", user.Id);

            return Ok(new { user.Id, user.Username, user.Userlogin });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto userLoginDto)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Userlogin == userLoginDto.Userlogin);
            if (user == null)
            {
                return BadRequest("Неправильний логін або пароль.");
            }
            if (user.Userpassword == userLoginDto.Userpassword)
            {
                user.Userpassword = _passwordHasher.HashPassword(user, userLoginDto.Userpassword);
                await _context.SaveChangesAsync();
            }
            else
            {
                var result = _passwordHasher.VerifyHashedPassword(user, user.Userpassword, userLoginDto.Userpassword);
                if (result == PasswordVerificationResult.Failed)
                {
                    return BadRequest("Неправильний логін або пароль.");
                }
            }

            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetInt32("User Id", user.Id);

            var token = GenerateJwtToken(user);

            return Ok(new { token });
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Userlogin)
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.Now.AddDays(60),
                signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        [HttpGet("profile")]
        [Authorize]
        public async Task<IActionResult> GetUserProfile()
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

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.Id.ToString() == userId);

            if (user == null)
            {
                return NotFound("Користувач не знайдений.");
            }

            return Ok(new
            {
                username = user.Username,
                image = string.IsNullOrEmpty(user.Userimage)
                    ? null
                    : "/" + user.Userimage.Replace("\\", "/") 
            });
        }

        [Authorize]
        [HttpGet("in-waiting")]
        public async Task<IActionResult> GetUsersInWaiting()
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var today = DateTime.Today;
            var fiveDaysFromNow = today.AddDays(5);

            var usersWithRelevantLibraries = await _context.Users
                .Include(u => u.Libraries)
                .Where(u => u.Libraries.Any(l =>
                    l.Status == "pending" || 
                    (l.Status == "active" && l.DataEndPlan.HasValue && l.DataEndPlan.Value.Date >= today && l.DataEndPlan.Value.Date <= fiveDaysFromNow)))
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Userlogin,
                    Userimage = string.IsNullOrEmpty(u.Userimage)
                        ? null
                        : "/" + u.Userimage.Replace("\\", "/"),
                    CreateAt = u.CreateAt.ToString("yyyy-MM-dd"),
                    Libraries = u.Libraries
                        .Where(l =>
                            l.Status == "pending" ||
                            (l.Status == "active" && l.DataEndPlan.HasValue && l.DataEndPlan.Value.Date >= today && l.DataEndPlan.Value.Date <= fiveDaysFromNow))
                        .Select(l => new
                        {
                            l.Id,
                            l.Status,
                            l.PlanId,
                            DataEndPlan = l.DataEndPlan.HasValue ? l.DataEndPlan.Value.ToString("yyyy-MM-dd") : null
                        })
                })
                .ToListAsync();

            return Ok(usersWithRelevantLibraries);
        }

        [Authorize]
        [HttpPost("check-payment")]
        public async Task<IActionResult> CheckPayment([FromBody] int libraryId)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.Id == libraryId);
            if (library == null)
            {
                return NotFound("Бібліотека не знайдена.");
            }

            if (library.DataEndPlan == null)
            {
                library.DataEndPlan = DateTime.Today.AddDays(30);
            }
            else
            {
                library.DataEndPlan = library.DataEndPlan.Value.AddDays(30);
            }

            library.Status = "active";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Платіж підтверджено", newEndDate = library.DataEndPlan.Value.ToString("yyyy-MM-dd") });
        }

        [Authorize]
        [HttpGet("user-list")]
        public async Task<IActionResult> GetActiveUsers()
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var users = await _context.Users
                .Include(u => u.Libraries)
                .Where(u => u.Libraries.Any(l => l.Status == "active"))
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Userlogin,
                    Userimage = string.IsNullOrEmpty(u.Userimage)
                        ? null
                        : "/" + u.Userimage.Replace("\\", "/"),
                    CreateAt = u.CreateAt.ToString("yyyy-MM-dd"),
                    Libraries = u.Libraries.Select(l => new
                    {
                        l.Id,
                        l.Status,
                        l.PlanId,
                        DataEndPlan = l.DataEndPlan.HasValue ? l.DataEndPlan.Value.ToString("yyyy-MM-dd") : null
                    })
                })
                .ToListAsync();

            return Ok(users);
        }

        [Authorize]
        [HttpGet("banned-list")]
        public async Task<IActionResult> GetBannedUsers()
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var today = DateTime.Today;

            var users = await _context.Users
                .Include(u => u.Libraries)
                .Where(u => u.Libraries.Any(l =>
                    (l.DataEndPlan.HasValue && today.Subtract(l.DataEndPlan.Value.Date).TotalDays >= 60) ||
                    (l.DataEndPlan == null && today.Subtract(u.CreateAt.Date).TotalDays >= 30)
                ))
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Userlogin,
                    Userimage = string.IsNullOrEmpty(u.Userimage)
                        ? null
                        : "/" + u.Userimage.Replace("\\", "/"),
                    CreateAt = u.CreateAt.ToString("yyyy-MM-dd"),
                    Libraries = u.Libraries.Select(l => new
                    {
                        l.Id,
                        l.Status,
                        l.PlanId,
                        DataEndPlan = l.DataEndPlan.HasValue ? l.DataEndPlan.Value.ToString("yyyy-MM-dd") : null
                    })
                })
                .ToListAsync();

            return Ok(users);
        }

        [Authorize]
        [HttpPost("ban-user")]
        public async Task<IActionResult> BanUser([FromBody] int libraryId)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.Id == libraryId);
            if (library == null)
            {
                return NotFound("Бібліотека не знайдена.");
            }

            library.Status = "banned";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Користувача заблоковано", status = library.Status });
        }

        [Authorize]
        [HttpGet("pending-list")]
        public async Task<IActionResult> GetPendingUsers()
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var today = DateTime.Today;

            var users = await _context.Users
                .Include(u => u.Libraries)
                .Where(u => u.Libraries.Any(l =>
                    l.DataEndPlan.HasValue && today.Subtract(l.DataEndPlan.Value.Date).TotalDays < 30 && today.Subtract(l.DataEndPlan.Value.Date).TotalDays > 0))
                .Select(u => new
                {
                    u.Id,
                    u.Username,
                    u.Userlogin,
                    Userimage = string.IsNullOrEmpty(u.Userimage)
                        ? null
                        : "/" + u.Userimage.Replace("\\", "/"),
                    CreateAt = u.CreateAt.ToString("yyyy-MM-dd"),
                    Libraries = u.Libraries.Select(l => new
                    {
                        l.Id,
                        l.Status,
                        l.PlanId,
                        DataEndPlan = l.DataEndPlan.HasValue ? l.DataEndPlan.Value.ToString("yyyy-MM-dd") : null
                    })
                })
                .ToListAsync();

            return Ok(users);
        }

        [Authorize]
        [HttpPost("mark-user-pending")]
        public async Task<IActionResult> MarkUserPending([FromBody] int libraryId)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            var library = await _context.Libraries.FirstOrDefaultAsync(l => l.Id == libraryId);
            if (library == null)
            {
                return NotFound("Бібліотека не знайдена.");
            }

            library.Status = "pending";
            await _context.SaveChangesAsync();

            return Ok(new { message = "Статус користувача змінено на 'pending'", status = library.Status });
        }

        [Authorize]
        [HttpPost("logout")]
        public IActionResult Logout()
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            HttpContext.Session.Clear();

            return Ok(new { message = "Вихід виконано успішно." });
        }

        [Authorize]
        [HttpPost("change-password")]
        public async Task<IActionResult> ChangePassword([FromBody] UserChangePasswordDto dto)
        {
            if (!Request.Headers.TryGetValue("X-Requested-From", out var origin) || origin != "BookCatApp")
            {
                return Unauthorized("Невірне джерело запиту.");
            }

            if (!Regex.IsMatch(dto.Email, @"^[^\s@]+@[^\s@]+\.[^\s@]+$"))
            {
                return BadRequest("Невірний формат електронної пошти.");
            }

            if (!Regex.IsMatch(dto.NewPassword, @"^[A-Za-z0-9]{8,20}$"))
            {
                return BadRequest("Пароль повинен містити 8–20 символів (англійські літери та цифри).");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Userlogin == dto.Email);
            if (user == null)
            {
                return NotFound("Користувача з такою поштою не знайдено.");
            }

            user.Userpassword = _passwordHasher.HashPassword(user, dto.NewPassword);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Пароль успішно змінено." });
        }


    }

}
