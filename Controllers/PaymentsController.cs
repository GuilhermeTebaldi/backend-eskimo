// CSharpAssistant.API/Controllers/PaymentsController.cs
using System;
using System.IO;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class PaymentsController : ControllerBase
    {
        private readonly AppDbContext _db;
        private readonly MercadoPagoService _mp;
        private readonly ILogger<PaymentsController> _logger;
        private readonly IConfiguration _config;

        public PaymentsController(
            AppDbContext db,
            MercadoPagoService mp,
            ILogger<PaymentsController> logger,
            IConfiguration config)
        {
            _db = db;
            _mp = mp;
            _logger = logger;
            _config = config;
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
                // Preferência por URL pública configurada (evita problemas atrás de proxy/prod)
                var baseUrl = _config["PublicBaseUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                {
                    // fallback: base da própria requisição
                    baseUrl = $"{Request.Scheme}://{Request.Host}";
                }

                var (url, prefId) = await _mp.CreateCheckoutPreferenceAsync(orderId, baseUrl);

                // (Opcional) Persistir referencia do pagamento no pedido:
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
        /// MP pode enviar GET (type=payment&data.id=xxx) ou POST (JSON).
        /// Aceita também HEAD/OPTIONS para evitar ruído.
        /// </summary>
        [HttpOptions("mp/webhook")]
        public IActionResult WebhookOptions() => Ok();

        [HttpHead("mp/webhook")]
        public IActionResult WebhookHead() => Ok();

        [HttpPost("mp/webhook")]
        [HttpGet("mp/webhook")]
        public async Task<IActionResult> MercadoPagoWebhook()
        {
            try
            {
                // 1) Extrai paymentId de:
                // - Query: ?type=payment&data.id=123 ou ?id=123
                // - Body JSON: { "data": { "id": "123" }, "type": "payment", ... }
                string? paymentId = null;

                if (Request.Query.TryGetValue("data.id", out var dataId) && !string.IsNullOrWhiteSpace(dataId))
                    paymentId = dataId.ToString();

                if (string.IsNullOrWhiteSpace(paymentId) && Request.Query.TryGetValue("id", out var id))
                    paymentId = id.ToString();

                if (string.IsNullOrWhiteSpace(paymentId) && (Request.ContentLength ?? 0) > 0)
                {
                    Request.EnableBuffering(); // permite ler o body sem quebrar o pipeline
                    using var reader = new StreamReader(Request.Body);
                    var bodyStr = await reader.ReadToEndAsync();
                    Request.Body.Position = 0;

                    if (!string.IsNullOrWhiteSpace(bodyStr))
                    {
                        using var json = System.Text.Json.JsonDocument.Parse(bodyStr);
                        var root = json.RootElement;
                        paymentId = root.GetPropertyOrDefault("data")?
                                         .GetPropertyOrDefault("id")?
                                         .GetString()
                                     ?? root.GetPropertyOrDefault("id")?.GetString();

                        // (Opcional) Log de segurança mínimo
                        var type = root.GetPropertyOrDefault("type")?.GetString();
                        _logger.LogInformation("Webhook MP body recebido. type={type}, paymentId={paymentId}", type, paymentId);
                    }
                }

                if (string.IsNullOrWhiteSpace(paymentId))
                {
                    _logger.LogWarning("Webhook Mercado Pago sem paymentId.");
                    // 200 para evitar retries infinitos do MP (evita ban)
                    return Ok();
                }

                // 2) Consulta o pagamento no MP (tentando todos os tokens ativos até achar)
                var statusInfo = await _mp.TryGetPaymentStatusAsync(paymentId);
                if (statusInfo == null)
                {
                    _logger.LogWarning("Não foi possível consultar pagamento {paymentId} em nenhuma conta configurada.", paymentId);
                    return Ok(); // retorna 200 para evitar retry agressivo; logamos para análise
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
                            // Evita regredir se já foi entregue
                            if (!string.Equals(order.Status, "entregue", StringComparison.OrdinalIgnoreCase))
                            {
                                order.Status = "pago";
                                await _db.SaveChangesAsync();
                                _logger.LogInformation("Order {orderId} marcado como PAGO via webhook MP (payment {paymentId}).", orderId, paymentId);
                            }
                        }
                        else if (string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Pagamento REJEITADO para order {orderId} (payment {paymentId}).", orderId, paymentId);
                            // (Opcional) order.Status = "cancelado";
                            // await _db.SaveChangesAsync();
                        }
                        else
                        {
                            _logger.LogInformation("Pagamento {paymentId} com status {status} (order {orderId}).", paymentId, status, orderId);
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
                // 200 para evitar reenvio agressivo; erro fica logado para análise.
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
