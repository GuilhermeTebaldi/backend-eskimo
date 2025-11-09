using System;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.DTOs;
using CSharpAssistant.API.Helpers;
using CSharpAssistant.API.Models;
using CSharpAssistant.API.Services;
using Microsoft.AspNetCore.Cors;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/store-customers")]
    [Route("api/storecustomers")]
    [EnableCors("AllowFrontend")]
    public class StoreCustomersController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly TokenService _tokenService;

        public StoreCustomersController(AppDbContext db, TokenService tokenService)
        {
            _db = db;
            _tokenService = tokenService;
        }

        [HttpPost("register")]
        [AllowAnonymous]
        public async Task<IActionResult> Register([FromBody] StoreCustomerRegisterDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (!string.Equals(dto.Password, dto.ConfirmPassword, StringComparison.Ordinal))
                return BadRequest(new { message = "As senhas não conferem." });

            var email = dto.Email.Trim().ToLowerInvariant();
            var exists = await _db.StoreCustomers.AnyAsync(c => c.Email == email);
            if (exists)
                return Conflict(new { message = "E-mail já cadastrado." });

            var nickname = string.IsNullOrWhiteSpace(dto.Nickname)
                ? dto.FullName
                : dto.Nickname;

            var customer = new StoreCustomer
            {
                Email = email,
                FullName = dto.FullName.Trim(),
                Nickname = nickname?.Trim() ?? string.Empty,
                PasswordHash = PasswordHasher.Hash(dto.Password),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _db.StoreCustomers.Add(customer);
            await _db.SaveChangesAsync();

            var token = _tokenService.GenerateStoreCustomerToken(customer);
            return Ok(new
            {
                token,
                customer = Map(customer)
            });
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] StoreCustomerLoginDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            var email = dto.Email.Trim().ToLowerInvariant();
            var customer = await _db.StoreCustomers.FirstOrDefaultAsync(c => c.Email == email);
            if (customer == null)
                return Unauthorized(new { message = "Credenciais inválidas." });

            if (!PasswordHasher.Verify(dto.Password, customer.PasswordHash))
                return Unauthorized(new { message = "Credenciais inválidas." });

            var token = _tokenService.GenerateStoreCustomerToken(customer);
            return Ok(new
            {
                token,
                customer = Map(customer)
            });
        }

        [HttpGet("me")]
        [Authorize(Roles = "store_customer")]
        public async Task<IActionResult> Me()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
                return Unauthorized(new { message = "Cliente não encontrado." });

            return Ok(Map(customer));
        }

        [HttpPut("me")]
        [Authorize(Roles = "store_customer")]
        public async Task<IActionResult> UpdateMe([FromBody] StoreCustomerProfileDTO dto)
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
                return Unauthorized(new { message = "Cliente não encontrado." });

            if (!string.IsNullOrWhiteSpace(dto.FullName))
                customer.FullName = dto.FullName.Trim();
            if (!string.IsNullOrWhiteSpace(dto.Nickname))
                customer.Nickname = dto.Nickname.Trim();

            customer.PhoneNumber = dto.PhoneNumber?.Trim();
            customer.Neighborhood = dto.Neighborhood?.Trim();
            customer.Street = dto.Street?.Trim();
            customer.Number = dto.Number?.Trim();
            customer.Complement = dto.Complement?.Trim();
            customer.AddressLabel = dto.AddressLabel?.Trim();
            if (!string.IsNullOrWhiteSpace(dto.ProfileImageBase64))
                customer.ProfileImageBase64 = dto.ProfileImageBase64;

            customer.UpdatedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            return Ok(Map(customer));
        }

        [HttpGet("me/orders")]
        [Authorize(Roles = "store_customer")]
        public async Task<IActionResult> MyOrders()
        {
            var customer = await GetCurrentCustomerAsync();
            if (customer == null)
                return Unauthorized(new { message = "Cliente não encontrado." });

            var orders = await _db.Orders
                .AsNoTracking()
                .Where(o => o.StoreCustomerId == customer.Id)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id,
                    o.Status,
                    o.Store,
                    o.Total,
                    o.CreatedAt,
                    o.DeliveryType,
                    o.PhoneNumber
                })
                .ToListAsync();

            return Ok(orders);
        }

        private async Task<StoreCustomer?> GetCurrentCustomerAsync()
        {
            var idClaim = User?.Claims?.FirstOrDefault(c =>
                c.Type == ClaimTypes.NameIdentifier)?.Value;

            if (!int.TryParse(idClaim, out var id))
                return null;

            return await _db.StoreCustomers.FirstOrDefaultAsync(c => c.Id == id);
        }

        private static StoreCustomerResponseDTO Map(StoreCustomer customer)
        {
            return new StoreCustomerResponseDTO
            {
                Id = customer.Id,
                Email = customer.Email,
                FullName = customer.FullName,
                Nickname = customer.Nickname,
                PhoneNumber = customer.PhoneNumber,
                Neighborhood = customer.Neighborhood,
                Street = customer.Street,
                Number = customer.Number,
                Complement = customer.Complement,
                AddressLabel = customer.AddressLabel,
                ProfileImageBase64 = customer.ProfileImageBase64,
                CreatedAt = customer.CreatedAt,
                UpdatedAt = customer.UpdatedAt
            };
        }
    }
}
