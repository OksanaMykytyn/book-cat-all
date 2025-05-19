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

namespace BookCatAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly BookCatDbContext _context;

        public UserController(BookCatDbContext context)
        {
            _context = context;
        }
        
        [HttpPost("register")]
        public async Task<IActionResult> Register(UserRegistrationDto userDto)
        {
            // Перевірка наявності користувача з таким же логіном
            if (await _context.Users.AnyAsync(u => u.Userlogin == userDto.Userlogin))
            {
                return BadRequest("Користувач з таким логіном вже існує.");
            }
            // Створення нового користувача
            var user = new User
            {
                Username = userDto.Username,
                Userlogin = userDto.Userlogin,
                Userpassword = userDto.Userpassword // Рекомендується хешувати пароль
            };
            _context.Users.Add(user);
            await _context.SaveChangesAsync();
            // Створення бібліотеки для нового користувача
            var library = new Library
            {
                UserId = user.Id,
                PlanId = userDto.PlanId,
                Status = "pending"
            };
            _context.Libraries.Add(library);
            await _context.SaveChangesAsync();

            // Зберегти дані користувача в сесії
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetInt32("User Id", user.Id);

            return Ok(new { user.Id, user.Username, user.Userlogin });
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] UserLoginDto userLoginDto)
        {
            // Знайти користувача за логіном
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Userlogin == userLoginDto.Userlogin);
            if (user == null)
            {
                return BadRequest("Неправильний логін або пароль.");
            }
            // Перевірка пароля
            if (user.Userpassword != userLoginDto.Userpassword) // Порівняння паролів у відкритому вигляді
            {
                return BadRequest("Неправильний логін або пароль.");
            }

            // Зберегти дані користувача в сесії
            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetInt32("User Id", user.Id);

            // Генерація JWT токена
            var token = GenerateJwtToken(user);

            // Успішний вхід
            return Ok(new { token });
        }

        private string GenerateJwtToken(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Userlogin)
            };
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("4f5g6h7j8k9l0m1n2o3p4q5r6s7t8u9v"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: "JwtIssuer",
                audience: "JwtAudience",
                claims: claims,
                expires: DateTime.Now.AddMinutes(30),
                signingCredentials: creds);
            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
