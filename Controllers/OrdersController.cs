using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CSharpAssistant.API.Models;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.DTOs;
using System.Linq;
using System.Threading.Tasks;

namespace CSharpAssistant.API.Models
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
            {
                return BadRequest(ModelState);
            }

            if (dto.Items == null || !dto.Items.Any())
            {
                return BadRequest(new { message = "O pedido deve conter pelo menos um item." });
            }

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
                {
                    return BadRequest(new { message = $"Estoque insuficiente para o produto {item.Name}. Quantidade disponível: {stock?.Quantity ?? 0}" });
                }
            }

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
                DeliveryFee = dto.DeliveryFee,
                Status = "pendente",
                PhoneNumber = dto.PhoneNumber,
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
                    order.PhoneNumber,
                    order.DeliveryFee,
                    order.CreatedAt,   // ✅ Data real para o frontend
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

    // mantém NoContent para não quebrar o front
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

            // mantém NoContent para não quebrar o front
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

    // Idempotência: se já estiver cancelado, não altera estoque de novo
    if (order.Status == "cancelado")
        return NoContent();

    // Transação simples pra garantir que ou tudo acontece, ou nada
    using var tx = await _context.Database.BeginTransactionAsync();

    // 🔁 Devolve estoque por item na loja do pedido
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

    // Marca como cancelado
    order.Status = "cancelado";

    await _context.SaveChangesAsync();
    await tx.CommitAsync();

    // mantém NoContent para não quebrar o front atual
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
                order.Total,
                order.DeliveryFee,
                order.PhoneNumber,
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
    }
}

