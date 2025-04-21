using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using e_commerce.Data;
using e_commerce.Models;
using e_commerce.Helpers;
using e_commerce.Services;
using e_commerce.DTOs;

namespace e_commerce.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly TokenService _tokenService;

        public AuthController(AppDbContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register()
        {
            try
            {
                var email = "admin@eskimo.com";
                var senha = "admin123";
                var username = "admin";

                if (await _context.Users.AnyAsync(u => u.Email == email))
                    return BadRequest("Admin já foi criado.");

                var user = new User
                {
                    Username = username,
                    Email = email,
                    Role = "admin",
                    PasswordHash = PasswordHasher.Hash(senha)
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Admin criado com sucesso" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { error = ex.Message, inner = ex.InnerException?.Message });
            }
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO loginDto)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);
            if (user == null || !PasswordHasher.Verify(loginDto.Password, user.PasswordHash))
                return Unauthorized("Credenciais inválidas.");

            var token = _tokenService.GenerateToken(user);

            return Ok(new { token });
        }
    }
}
