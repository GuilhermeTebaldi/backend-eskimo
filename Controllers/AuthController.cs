[HttpPost("register")]
using Microsoft.AspNetCore.Mvc;

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
