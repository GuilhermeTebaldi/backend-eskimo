using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using e_commerce.Models;
using e_commerce.Data;
using e_commerce.DTOs;
using System.Linq;

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
        public IActionResult CreateOrder(OrderDTO dto)
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
                Status = "pendente",
                PhoneNumber = dto.PhoneNumber, // ✅ AQUI ADICIONADO
                Items = dto.Items.Select(i => new OrderItem
                {
                    ProductId = i.ProductId,
                    Name = i.Name,
                    Price = i.Price,
                    Quantity = i.Quantity
                }).ToList()
            };

            _context.Orders.Add(order);
            _context.SaveChanges();

            return Ok(new { message = "Pedido salvo com sucesso!" });
        }

        // 🟡 GET: Listar todos os pedidos
        [HttpGet]
        public IActionResult GetAllOrders()
        {
            var orders = _context.Orders
                .Include(o => o.Items)
                .OrderByDescending(o => o.Id)
                .Select(order => new
                {
                    order.Id,
                    order.CustomerName,
                    order.DeliveryType,
                    order.Address,
                    order.Street,
                    order.Number,
                    order.Complement,
                    order.Store,
                    order.Total,
                    order.Status,
                    order.PhoneNumber, // ✅ Mostrar telefone também
                    Items = order.Items.Select(item => new
                    {
                        item.ProductId,
                        item.Name,
                        item.Price,
                        item.Quantity
                    }).ToList()
                })
                .ToList();

            return Ok(orders);
        }

        // 🟢 PATCH: Confirmar pagamento
        [HttpPatch("{id}/confirm")]
        public async Task<IActionResult> ConfirmOrder(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null) return NotFound();

            order.Status = "pago";
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

            if (order == null) return NotFound();

            _context.OrderItems.RemoveRange(order.Items);
            _context.Orders.Remove(order);
            await _context.SaveChangesAsync();

            return NoContent();
        }
    }
}
