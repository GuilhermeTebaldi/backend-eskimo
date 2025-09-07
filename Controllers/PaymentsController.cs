// CSharpAssistant.API/Controllers/PaymentsController.cs
using System;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly MercadoPagoService _mp;
        private readonly ILogger<PaymentsController> _logger;

        public PaymentsController(AppDbContext db, MercadoPagoService mp, ILogger<PaymentsController> logger)
        {
            _db = db;
            _mp = mp;
            _logger = logger;
        }

        /// <summary>
        /// Saúde do serviço (útil pro front decidir exibir botão do MP).
        /// </summary>
        [HttpGet("mp/ping")]
        public IActionResult Ping() => Ok(new { ok = true, provider = "mercadopago" });

        /// <summary>
        /// Cria uma Preference de checkout do Mercado Pago para um orderId existente.
        /// Front deve redirecionar para "url" (init_point).
        /// </summary>
        [HttpPost("mp/checkout")]
        public async Task<IActionResult> CreateMercadoPagoCheckout([FromQuery] int orderId)
        {
            if (orderId <= 0) return BadRequest(new { message = "orderId inválido." });

            try
            {
                var baseUrl = $"{Request.Scheme}://{Request.Host}";
                var (url, prefId) = await _mp.CreateCheckoutPreferenceAsync(orderId, baseUrl);

                // (Opcional) Armazenar provider/prefId no Order se você tiver campos pra isso.
                // var order = await _db.Orders.FindAsync(orderId);
                // if (order != null) { order.PaymentProvider = "mercadopago"; order.PaymentReference = prefId; await _db.SaveChangesAsync(); }

                return Ok(new
                {
                    type = "init_point",
                    url,
                    preferenceId = prefId
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar checkout do Mercado Pago para orderId={orderId}", orderId);
                return Problem($"Falha ao iniciar pagamento: {ex.Message}");
            }
        }

        /// <summary>
        /// Webhook do Mercado Pago — recebe notificações de pagamento.
        /// MP envia GET (com query type=payment&data.id=xxx) ou POST dependendo da configuração.
        /// </summary>
        [HttpPost("mp/webhook")]
        [HttpGet("mp/webhook")]
        public async Task<IActionResult> MercadoPagoWebhook()
        {
            try
            {
                // 1) Extrai paymentId (GET ou POST)
                string? paymentId = null;

                // GET: ?type=payment&data.id=123
                if (Request.Query.TryGetValue("data.id", out var dataId) && !string.IsNullOrWhiteSpace(dataId))
                    paymentId = dataId.ToString();

                if (string.IsNullOrWhiteSpace(paymentId) && Request.Query.TryGetValue("id", out var id))
                    paymentId = id.ToString();

                // POST (JSON): { "data": { "id": "123" }, "type": "payment", ... }
                if (string.IsNullOrWhiteSpace(paymentId) && Request.ContentLength > 0)
                {
                    using var reader = new System.IO.StreamReader(Request.Body);
                    var bodyStr = await reader.ReadToEndAsync();
                    if (!string.IsNullOrWhiteSpace(bodyStr))
                    {
                        using var json = System.Text.Json.JsonDocument.Parse(bodyStr);
                        var root = json.RootElement;
                        paymentId = root.GetPropertyOrDefault("data")?
                                         .GetPropertyOrDefault("id")?
                                         .GetString()
                                     ?? root.GetPropertyOrDefault("id")?.GetString();
                    }
                }

                if (string.IsNullOrWhiteSpace(paymentId))
                {
                    _logger.LogWarning("Webhook Mercado Pago sem paymentId.");
                    return Ok(); // responde 200 para evitar retries infinitos
                }

                // 2) Consulta o pagamento no MP (tentando todos os tokens ativos até achar)
                var statusInfo = await _mp.TryGetPaymentStatusAsync(paymentId);
                if (statusInfo == null)
                {
                    _logger.LogWarning("Não foi possível consultar pagamento {paymentId} em nenhuma conta configurada.", paymentId);
                    return Ok(); // responde 200 mesmo assim
                }

                var (status, externalReference) = statusInfo.Value;

                // 3) Se tiver external_reference, localiza o pedido e atualiza status
                if (int.TryParse(externalReference, out var orderId))
                {
                    var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
                    if (order != null)
                    {
                        if (string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.Equals(order.Status, "entregue", StringComparison.OrdinalIgnoreCase))
                            {
                                order.Status = "pago"; // marca como pago
                                await _db.SaveChangesAsync();
                                _logger.LogInformation("Order {orderId} marcado como pago via webhook MP (payment {paymentId}).", orderId, paymentId);
                            }
                        }
                        else if (string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase))
                        {
                            // opcional: marcar como cancelado/rejeitado
                            _logger.LogInformation("Pagamento rejeitado para order {orderId} (payment {paymentId}).", orderId, paymentId);
                        }
                        else
                        {
                            _logger.LogInformation("Pagamento {paymentId} com status {status} para order {orderId}.", paymentId, status, orderId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Webhook MP: order {orderId} não encontrado (payment {paymentId}).", orderId, paymentId);
                    }
                }
                else
                {
                    _logger.LogWarning("Webhook MP: external_reference inválido: {externalReference}", externalReference);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao processar webhook do Mercado Pago.");
                // Retorne 200 para evitar reenvio agressivo do MP; logamos o erro para análise.
                return Ok();
            }
        }
    }

    internal static class JsonElementExt
    {
        public static System.Text.Json.JsonElement? GetPropertyOrDefault(this System.Text.Json.JsonElement element, string name)
        {
            if (element.ValueKind != System.Text.Json.JsonValueKind.Object) return null;
            if (element.TryGetProperty(name, out var value)) return value;
            return null;
        }
    }
}
