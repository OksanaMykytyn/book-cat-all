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
                Userpassword = userDto.Userpassword 
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

            HttpContext.Session.SetString("Username", user.Username);
            HttpContext.Session.SetInt32("User Id", user.Id);

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
            if (user.Userpassword != userLoginDto.Userpassword) 
            {
                return BadRequest("Неправильний логін або пароль.");
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
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes("4f5g6h7j8k9l0m1n2o3p4q5r6s7t8u9v"));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: "JwtIssuer",
                audience: "JwtAudience",
                claims: claims,
                expires: DateTime.Now.AddDays(30),
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
                username = user.Username
            });
        }
    }


}
