using Microsoft.AspNetCore.Mvc;
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

        [HttpGet]
        public IActionResult GetAllOrders()
        {
            var orders = _context.Orders
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
    }
}
