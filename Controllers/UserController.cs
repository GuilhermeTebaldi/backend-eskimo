using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using CSharpAssistant.API.Helpers;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Policy = "RequireAdmin")]
    public class UserController : ControllerBase
    {
        private readonly AppDbContext _db;
        public UserController(AppDbContext db) => _db = db;

        // GET: /api/user
        [HttpGet]
        public async Task<IActionResult> GetAll()
        {
            var users = await _db.Users
                .AsNoTracking()
                .Select(u => new {
                    u.Id, u.Username, u.Email, u.Role, u.IsEnabled, u.Permissions
                })
                .ToListAsync();
            return Ok(users);
        }

        // POST: /api/user  (criar)
        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateUserRequest body)
        {
            if (string.IsNullOrWhiteSpace(body.Email) || string.IsNullOrWhiteSpace(body.Password))
                return BadRequest("Email e senha são obrigatórios.");

            if (await _db.Users.AnyAsync(u => u.Email == body.Email))
                return Conflict("Email já cadastrado.");

            var user = new User
            {
                Username = string.IsNullOrWhiteSpace(body.Username) ? body.Email : body.Username,
                Email = body.Email,
                Role = string.IsNullOrWhiteSpace(body.Role) ? "operator" : body.Role,
                IsEnabled = body.IsEnabled ?? true,
                Permissions = string.IsNullOrWhiteSpace(body.PermissionsJson) ? "{}" : body.PermissionsJson,
                PasswordHash = PasswordHasher.Hash(body.Password)
            };

            _db.Users.Add(user);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Usuário criado.", id = user.Id });
        }

        // PUT: /api/user/{id}  (editar dados comuns + redefinir senha opcional)
        [HttpPut("{id}")]
        public async Task<IActionResult> Update(int id, [FromBody] UpdateUserRequest body)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();

            if (!string.IsNullOrWhiteSpace(body.Email))
                user.Email = body.Email;
            if (!string.IsNullOrWhiteSpace(body.Username))
                user.Username = body.Username;
            if (!string.IsNullOrWhiteSpace(body.Role))
                user.Role = body.Role;
            if (body.IsEnabled.HasValue)
                user.IsEnabled = body.IsEnabled.Value;
            if (!string.IsNullOrWhiteSpace(body.PermissionsJson))
                user.Permissions = body.PermissionsJson;
            if (!string.IsNullOrWhiteSpace(body.NewPassword))
                user.PasswordHash = PasswordHasher.Hash(body.NewPassword);

            await _db.SaveChangesAsync();
            return Ok(new { message = "Usuário atualizado." });
        }

        // DELETE: /api/user/{id}
        [HttpDelete("{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            _db.Users.Remove(user);
            await _db.SaveChangesAsync();
            return Ok(new { message = "Usuário removido." });
        }

        // PATCH: /api/user/{id}/permissions   (editar granular via JSON)
        [HttpPatch("{id}/permissions")]
        public async Task<IActionResult> PatchPermissions(int id, [FromBody] string permissionsJson)
        {
            var user = await _db.Users.FindAsync(id);
            if (user == null) return NotFound();
            user.Permissions = string.IsNullOrWhiteSpace(permissionsJson) ? "{}" : permissionsJson;
            await _db.SaveChangesAsync();
            return Ok(new { message = "Permissões atualizadas." });
        }
    }

    public class CreateUserRequest
    {
        public string? Username { get; set; }
        public string Email { get; set; } = "";
        public string Password { get; set; } = "";
        public string? Role { get; set; } // "admin" | "operator"
        public bool? IsEnabled { get; set; }
        // JSON livre. Ex.: {"can_manage_products":true,"stores":{"efapi":{"orders":true,"edit_stock":true}}}
        public string? PermissionsJson { get; set; }
    }

    public class UpdateUserRequest
    {
        public string? Username { get; set; }
        public string? Email { get; set; }
        public string? Role { get; set; }
        public bool? IsEnabled { get; set; }
        public string? PermissionsJson { get; set; }
        public string? NewPassword { get; set; } // opcional
    }
}
