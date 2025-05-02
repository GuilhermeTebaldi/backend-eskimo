using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using e_commerce.Models;
using e_commerce.Data;
using e_commerce.DTOs;
using System.Linq;
using System.Threading.Tasks;

namespace e_commerce.Controllers
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
        public async Task<IActionResult> CreateOrder(OrderDTO dto)
        {
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

            order.Status = "entregue";
            await _context.SaveChangesAsync();

            return NoContent();
        }

        // 🔴 DELETE: Excluir pedido
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
    }
}
