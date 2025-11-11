using System;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CSharpAssistant.API.Models;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.DTOs;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class OrdersController : ControllerBase
    {
        private readonly AppDbContext _context;

        public OrdersController(AppDbContext context)
        {
            _context = context;
        }

        // 🔴 POST: Criar novo pedido
        [HttpPost]
        public async Task<IActionResult> CreateOrder([FromBody] OrderCreateDTO dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            if (dto.Items == null || !dto.Items.Any())
                return BadRequest(new { message = "O pedido deve conter pelo menos um item." });

            // ✅ Validação condicional para pedidos de entrega
            if (dto.DeliveryType == "entregar")
            {
                if (string.IsNullOrWhiteSpace(dto.Address) ||
                    string.IsNullOrWhiteSpace(dto.Street) ||
                    string.IsNullOrWhiteSpace(dto.Number) ||
                    string.IsNullOrWhiteSpace(dto.PhoneNumber))
                {
                    return BadRequest(new { message = "Endereço completo e telefone são obrigatórios para entrega." });
                }
            }

            // 🔒 Validação de estoque antes de criar o pedido
            foreach (var item in dto.Items)
            {
                var stock = await _context.StoreStocks
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.Store == dto.Store);

                if (stock == null || stock.Quantity < item.Quantity)
                    return BadRequest(new { message = $"Estoque insuficiente para o produto {item.Name}. Quantidade disponível: {stock?.Quantity ?? 0}" });
            }

            // 💰 Calcula frete impondo mínimo no servidor (fonte da verdade)
            decimal fee = 0m;
            if (dto.DeliveryType == "entregar")
            {
                var settings = await _context.Settings.FirstOrDefaultAsync();
                var min = settings?.MinDelivery ?? 0m;

                var clientFee = dto.DeliveryFee > 0m ? dto.DeliveryFee : 0m;
                fee = clientFee > min ? clientFee : min; // max(cliente, mínimo)
            }

            var normalizedPaymentMethod = string.IsNullOrWhiteSpace(dto.PaymentMethod)
                ? "mercado_pago"
                : dto.PaymentMethod.Trim().ToLowerInvariant();
            if (normalizedPaymentMethod != "mercado_pago" && normalizedPaymentMethod != "cash")
                normalizedPaymentMethod = "mercado_pago";

            var order = new Order
            {
                CustomerName = dto.CustomerName,
                DeliveryType = dto.DeliveryType,
                Address = dto.Address,
                Street = dto.Street,
                Number = dto.Number,
                Complement = dto.Complement,
                Store = dto.Store,
                Total = dto.Total,
                DeliveryFee = fee,
                Status = "pendente",
                PaymentMethod = normalizedPaymentMethod,
                PhoneNumber = dto.PhoneNumber,
                StoreCustomerId = GetStoreCustomerId(),
                Items = dto.Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    Name = i.Name,
                    Price = i.Price,
                    Quantity = i.Quantity,
                    ImageUrl = i.ImageUrl,
                    Store = dto.Store
                }).ToList()
            };

            _context.Orders.Add(order);
            await _context.SaveChangesAsync();

            // ✅ Descontar estoque da loja
            foreach (var item in order.Items)
            {
                var stock = await _context.StoreStocks
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.Store == order.Store);

                if (stock != null)
                {
                    stock.Quantity -= item.Quantity;
                    if (stock.Quantity < 0) stock.Quantity = 0;
                }
            }

            await _context.SaveChangesAsync();

            return Ok(new { id = order.Id, message = "Pedido salvo com sucesso!" });
        }

        // 🟡 GET: Listar todos os pedidos
        [HttpGet]
        public async Task<IActionResult> GetAllOrders()
        {
            var orders = await _context.Orders
                .Include(o => o.Items)
                .OrderByDescending(o => o.Id)
                .Select(order => new
                {
                    order.Id,
                    order.CustomerName,
                    name = order.CustomerName,
                    order.DeliveryType,
                    order.Address,
                    order.Street,
                    order.Number,
                    order.Complement,
                    order.Store,
                    order.Total,
                    order.Status,
                    order.PaymentMethod,
                    order.StoreCustomerId,
                    order.PhoneNumber,
                    order.DeliveryFee,
                    order.CreatedAt,
                    order.WhatsappNotifiedAt,
                    Items = order.Items.Select(item => new
                    {
                        item.ProductId,
                        item.Name,
                        item.Price,
                        item.Quantity,
                        item.ImageUrl,
                        item.Store
                    }).ToList()
                })
                .ToListAsync();

            return Ok(orders);
        }

        // 🟢 PATCH: Confirmar pagamento
        [HttpPatch("{id}/confirm")]
        public async Task<IActionResult> ConfirmOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new { message = "Pedido não encontrado." });

            if (order.Status == "entregue")
                return BadRequest(new { message = "Pedido já foi entregue." });

            if (order.Status == "cancelado")
                return BadRequest(new { message = "Pedido cancelado não pode ser confirmado." });

            order.Status = "pago";
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // 🟢 PATCH: Marcar como entregue
        [HttpPatch("{id}/deliver")]
        public async Task<IActionResult> DeliverOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new { message = "Pedido não encontrado." });

            if (order.Status == "cancelado")
                return BadRequest(new { message = "Pedido cancelado não pode ser entregue." });

            order.Status = "entregue";
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // 🛑 PATCH: Cancelar pedido (devolve estoque) — idempotente e seguro
        [HttpPatch("{id}/cancel")]
        public async Task<IActionResult> CancelOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "Pedido não encontrado." });

            if (order.Status == "entregue")
                return BadRequest(new { message = "Pedido já entregue não pode ser cancelado." });

            if (order.Status == "cancelado")
                return NoContent();

            using var tx = await _context.Database.BeginTransactionAsync();

            foreach (var item in order.Items)
            {
                var stock = await _context.StoreStocks
                    .FirstOrDefaultAsync(s => s.ProductId == item.ProductId && s.Store == order.Store);

                if (stock == null)
                {
                    stock = new StoreStock
                    {
                        ProductId = item.ProductId,
                        Store = order.Store,
                        Quantity = 0
                    };
                    _context.StoreStocks.Add(stock);
                }

                stock.Quantity += item.Quantity;
            }

            order.Status = "cancelado";

            await _context.SaveChangesAsync();
            await tx.CommitAsync();

            return NoContent();
        }

        // 🔴 DELETE: Excluir pedido individual
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeleteOrder(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "Pedido não encontrado." });

            _context.OrderItems.RemoveRange(order.Items);
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // 🔥 NOVO: DELETE TODOS OS PEDIDOS
        [HttpDelete("clear")]
        public async Task<IActionResult> ClearOrders()
        {
            var allOrders = await _context.Orders.Include(o => o.Items).ToListAsync();

            if (allOrders.Count == 0)
                return NoContent();

            _context.OrderItems.RemoveRange(allOrders.SelectMany(o => o.Items));
            _context.Orders.RemoveRange(allOrders);

            await _context.SaveChangesAsync();
            return Ok(new { message = "Todos os pedidos foram excluídos com sucesso." });
        }

        // 🟢 GET: Pedidos do cliente autenticado
        [HttpGet("my")]
        [Authorize(Roles = "store_customer")]
        public async Task<IActionResult> GetMyOrders()
        {
            var customerId = GetStoreCustomerId();
            if (customerId == null)
                return Unauthorized(new { message = "Cliente não identificado." });

            var orders = await _context.Orders
                .AsNoTracking()
                .Where(o => o.StoreCustomerId == customerId)
                .OrderByDescending(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id,
                    o.Status,
                    o.PaymentMethod,
                    o.Store,
                    o.Total,
                    o.CreatedAt,
                    o.DeliveryType,
                    o.PhoneNumber
                })
                .ToListAsync();

            return Ok(orders);
        }

        // 🟢 GET: Buscar pedido por ID (para polling no front)
        [HttpGet("{id}")]
        public async Task<IActionResult> GetOrderById(int id)
        {
            var order = await _context.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "Pedido não encontrado." });

            return Ok(new
            {
                order.Id,
                order.Status,
                order.Store,
                order.CustomerName,
                order.StoreCustomerId,
                order.Total,
                order.CreatedAt,
                order.DeliveryType,
                order.DeliveryFee,
                order.PaymentMethod,
                order.PhoneNumber,
                order.WhatsappNotifiedAt,
                Items = order.Items.Select(i => new
                {
                    i.ProductId,
                    i.Name,
                    i.Price,
                    i.Quantity,
                    i.ImageUrl,
                    i.Store
                }).ToList()
            });
        }

        private int? GetStoreCustomerId()
        {
            var role = User?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.Role || c.Type == "role")?.Value;
            if (string.IsNullOrWhiteSpace(role) || !role.Equals("store_customer", System.StringComparison.OrdinalIgnoreCase))
                return null;

            var idClaim = User?.Claims?.FirstOrDefault(c => c.Type == ClaimTypes.NameIdentifier)?.Value;
            if (int.TryParse(idClaim, out var id))
                return id;
            return null;
        }
    }
}
