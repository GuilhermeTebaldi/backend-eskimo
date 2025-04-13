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
        public async Task<IActionResult> Register([FromBody] RegisterDTO registerDto)
        {
            if (await _context.Users.AnyAsync(u => u.Email == registerDto.Email))
                return BadRequest("Email já cadastrado.");

            var user = new User
            {
                Username = registerDto.Username,
                Email = registerDto.Email,
                Role = registerDto.Role,
                PasswordHash = PasswordHasher.Hash(registerDto.Password)
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new { message = "Usuário criado com sucesso" });
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