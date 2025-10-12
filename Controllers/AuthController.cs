using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using CSharpAssistant.API.Helpers;
using CSharpAssistant.API.Services;
using CSharpAssistant.API.DTOs;

namespace CSharpAssistant.API.Controllers
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
    Console.WriteLine("🔐 Tentativa de login com:");
    Console.WriteLine($"Email: {loginDto.Email}");
    Console.WriteLine($"Password: {loginDto.Password}");

    Console.WriteLine("🔎 Consultando usuário no banco...");
    var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == loginDto.Email);

    if (user == null)
    {
        Console.WriteLine("❌ Usuário não encontrado no banco.");
        return Unauthorized("Credenciais inválidas.");
    }
    else
    {
        Console.WriteLine("✅ Usuário encontrado: " + user.Email);
    }
if (!user.IsEnabled)
{
    Console.WriteLine("❌ Usuário desabilitado.");
    return Unauthorized("Usuário desabilitado pelo administrador.");
}

    if (!PasswordHasher.Verify(loginDto.Password, user.PasswordHash))
    {
        Console.WriteLine("❌ Senha incorreta.");
        return Unauthorized("Credenciais inválidas.");
    }

    var token = _tokenService.GenerateToken(user);
    Console.WriteLine("✅ Token gerado com sucesso.");

    return Ok(new { token });
}

    }
}
