using System;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PrinterController : ControllerBase
    {
        private const string PrinterHeaderName = "X-Printer-Key";
        private readonly AppDbContext _context;
        private readonly string? _configuredPrinterKey;

        public PrinterController(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuredPrinterKey = configuration["Printer:Key"];
        }

        private bool TryAuthorizePrinter(out IActionResult? unauthorizedResult)
        {
            unauthorizedResult = null;
            if (string.IsNullOrWhiteSpace(_configuredPrinterKey))
                return true;

            if (!Request.Headers.TryGetValue(PrinterHeaderName, out var headerValues))
            {
                unauthorizedResult = Unauthorized(new { message = "Chave da impressora ausente." });
                return false;
            }

            if (!string.Equals(headerValues.FirstOrDefault(), _configuredPrinterKey, StringComparison.Ordinal))
            {
                unauthorizedResult = Unauthorized(new { message = "Chave da impressora inválida." });
                return false;
            }

            return true;
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPendingOrder([FromQuery] string store)
        {
            if (!TryAuthorizePrinter(out var unauthorized))
                return unauthorized!;

            if (string.IsNullOrWhiteSpace(store))
                return BadRequest(new { message = "Loja é obrigatória." });

            var normalizedStore = store.Trim().ToLowerInvariant();

            var pending = await _context.Orders
                .AsNoTracking()
                .Where(o =>
                    o.PrintedAtUtc == null &&
                    o.Store != null &&
                    o.Store.ToLower() == normalizedStore &&
                    o.Status != "cancelado" &&
                    o.Status != "entregue" &&
                    (o.PaymentMethod == "cash" || o.Status == "pago"))
                .OrderBy(o => o.CreatedAt)
                .Select(o => new
                {
                    o.Id,
                    o.Store,
                    o.Status,
                    o.PaymentMethod
                })
                .FirstOrDefaultAsync();

            if (pending == null)
                return NoContent();

            return Ok(pending);
        }

        [HttpPost("mark-printed/{id}")]
        public async Task<IActionResult> MarkPrinted(int id, [FromBody] MarkPrintedRequest request)
        {
            if (!TryAuthorizePrinter(out var unauthorized))
                return unauthorized!;

            if (request == null)
                return BadRequest(new { message = "Requisição inválida." });

            if (string.IsNullOrWhiteSpace(request.Store))
                return BadRequest(new { message = "Loja é obrigatória." });

            var normalizedStore = request.Store.Trim();

            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new { message = "Pedido não encontrado." });

            if (!string.Equals(order.Store?.Trim(), normalizedStore, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "A loja da requisição não corresponde ao pedido." });

            if (order.PrintedAtUtc != null)
                return Ok(new { id = order.Id, alreadyPrinted = true });

            var reason = order.PaymentMethod == "cash" ? "cash_created" : "paid_online";

            order.PrintedAtUtc = DateTime.UtcNow;
            order.PrintReason = reason;
            order.PrintedBy = request.ClientId;
            order.PrintCopies = Math.Clamp(request.Copies, 1, 3);
            order.LastPrintError = null;

            await _context.SaveChangesAsync();

            return Ok(new { id = order.Id, printedAt = order.PrintedAtUtc });
        }

        [HttpPost("mark-failed/{id}")]
        public async Task<IActionResult> MarkFailed(int id, [FromBody] MarkFailedRequest request)
        {
            if (!TryAuthorizePrinter(out var unauthorized))
                return unauthorized!;

            if (request == null)
                return BadRequest(new { message = "Requisição inválida." });

            if (string.IsNullOrWhiteSpace(request.Store))
                return BadRequest(new { message = "Loja é obrigatória." });

            var normalizedStore = request.Store.Trim();
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new { message = "Pedido não encontrado." });

            if (!string.Equals(order.Store?.Trim(), normalizedStore, StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { message = "A loja da requisição não corresponde ao pedido." });

            order.LastPrintError = string.IsNullOrWhiteSpace(request.PrinterName)
                ? request.Error
                : $"[{request.PrinterName}] {request.Error}";
            order.PrintedBy = request.ClientId;

            await _context.SaveChangesAsync();

            return Ok(new { id = order.Id });
        }

        [Authorize(Policy = "RequireAdmin")]
        [HttpPost("reprint/{id}")]
        public async Task<IActionResult> RequestManualReprint(int id)
        {
            var order = await _context.Orders.FindAsync(id);
            if (order == null)
                return NotFound(new { message = "Pedido não encontrado." });

            order.PrintedAtUtc = null;
            order.PrintReason = "manual_reprint_requested";
            order.PrintedBy = null;
            order.PrintCopies = null;
            order.LastPrintError = null;

            await _context.SaveChangesAsync();
            return Ok(new { id = order.Id });
        }

        [Authorize(Policy = "RequireAdmin")]
        [HttpPost("mark-printed-bulk")]
        public async Task<IActionResult> MarkPrintedBulk([FromBody] MarkPrintedBulkRequest request)
        {
            if (request == null)
                return BadRequest(new { message = "Requisição inválida." });

            if (string.IsNullOrWhiteSpace(request.Store))
                return BadRequest(new { message = "Loja é obrigatória." });

            if (request.BeforeUtc == default)
                return BadRequest(new { message = "beforeUtc é obrigatório." });

            var store = request.Store.Trim().ToLowerInvariant();

            var orders = await _context.Orders
                .Where(o =>
                    o.PrintedAtUtc == null &&
                    o.Store != null &&
                    o.Store.ToLower() == store &&
                    o.CreatedAt <= request.BeforeUtc)
                .ToListAsync();

            var now = DateTime.UtcNow;
            foreach (var o in orders)
            {
                o.PrintedAtUtc = now;
                o.PrintReason = string.IsNullOrWhiteSpace(request.Reason) ? "bootstrap_skip" : request.Reason.Trim();
                o.PrintedBy = string.IsNullOrWhiteSpace(request.ClientId) ? "admin-bulk" : request.ClientId.Trim();
                o.PrintCopies = null;
                o.LastPrintError = null;
            }

            await _context.SaveChangesAsync();
            return Ok(new { updated = orders.Count });
        }

        [HttpGet("/api/orders/{id}/receipt")]
        public async Task<IActionResult> GetReceipt(int id)
        {
            if (!TryAuthorizePrinter(out var unauthorized))
                return unauthorized!;

            var order = await _context.Orders
                .AsNoTracking()
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
                return NotFound(new { message = "Pedido não encontrado." });

            var receipt = BuildReceipt(order);
            return Content(receipt, "text/plain");
        }

        private static string BuildReceipt(Order order)
        {
            var builder = new StringBuilder();
            builder.Append("\u001b\u0061\u0001"); // center
            builder.AppendLine("ESKIMÓ SORVETES");
            builder.AppendLine($"CHAPECÓ – {order.Store?.ToUpperInvariant() ?? "LOJA"}");
            builder.Append("\u001b\u0061\u0000"); // left
            builder.Append("\u001b\u0021\u0011"); // double size
            builder.AppendLine($"PEDIDO #{order.Id}");
            builder.Append("\u001b\u0021\u0000"); // normal
            builder.AppendLine($"CLIENTE: {order.CustomerName}");

            var addressLines = new[]
            {
                order.Address,
                string.Join(" ", new[] { order.Street, order.Number }.Where(p => !string.IsNullOrWhiteSpace(p))),
                order.Complement
            }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x!.Trim())
            .ToList();

            foreach (var line in addressLines)
            {
                builder.AppendLine(line);
            }

            builder.AppendLine(new string('-', 32));
            builder.AppendLine("ITENS:");

            void AppendLineWithAmounts(string label, string amount)
            {
                const int width = 32;
                var trimmedLabel = label.Length > width - amount.Length ? label.Substring(0, Math.Max(0, width - amount.Length)) : label;
                var padding = width - trimmedLabel.Length - amount.Length;
                var spaces = padding > 0 ? new string(' ', padding) : string.Empty;
                builder.AppendLine(trimmedLabel + spaces + amount);
            }

            decimal itemsTotal = 0;
            foreach (var item in order.Items ?? Enumerable.Empty<OrderItem>())
            {
                var subtotal = item.Price * item.Quantity;
                itemsTotal += subtotal;
                var lineLabel = $"{item.Quantity}x {item.Name}";
                var amountText = $"R$ {subtotal.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"))}";
                AppendLineWithAmounts(lineLabel, amountText);
            }

            if (order.DeliveryFee > 0)
            {
                AppendLineWithAmounts("Taxa de entrega", $"R$ {order.DeliveryFee.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"))}");
            }

            builder.AppendLine(new string('-', 32));

            AppendLineWithAmounts("TOTAL", $"R$ {order.Total.ToString("0.00", CultureInfo.GetCultureInfo("pt-BR"))}");

            var paymentLabel = order.PaymentMethod == "cash"
                ? "PAGAMENTO NO LOCAL"
                : "MERCADO PAGO (ONLINE)";
            builder.AppendLine(paymentLabel);

            var timestamp = order.CreatedAt.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
            builder.AppendLine($"DATA/HORA: {timestamp}");
            builder.AppendLine("OBRIGADO E VOLTE SEMPRE");
            builder.AppendLine("\u001dV\u0001"); // cut command

            return builder.ToString();
        }

        public class MarkPrintedRequest
        {
            public string Store { get; set; } = string.Empty;
            public string ClientId { get; set; } = string.Empty;
            public string PrinterName { get; set; } = string.Empty;
            public int Copies { get; set; } = 1;
        }

        public class MarkFailedRequest
        {
            public string Store { get; set; } = string.Empty;
            public string ClientId { get; set; } = string.Empty;
            public string PrinterName { get; set; } = string.Empty;
            public string Error { get; set; } = string.Empty;
        }

        public class MarkPrintedBulkRequest
        {
            public string Store { get; set; } = string.Empty;
            public DateTime BeforeUtc { get; set; }
            public string? ClientId { get; set; }
            public string? PrinterName { get; set; }
            public string? Reason { get; set; }
        }
    }
}
