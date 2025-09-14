// CSharpAssistant.API/Controllers/PaymentsController.cs
using System;
using System.IO;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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

        // Saúde do serviço
        [HttpGet("mp/ping")]
        public IActionResult Ping() => Ok(new { ok = true, provider = "mercadopago" });

        /// <summary>
        /// Cria a Preference no MP para um orderId e retorna a URL (init_point).
        /// O front PODE usar diretamente essa URL, mas recomendamos usar /mp/go para navegação em mesma aba.
        /// </summary>
        [HttpPost("mp/checkout")]
        public async Task<IActionResult> CreateMercadoPagoCheckout([FromQuery] int orderId)
        {
            if (orderId <= 0) return BadRequest(new { message = "orderId inválido." });

            try
            {
                var baseUrl = _config["PublicBaseUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                    baseUrl = $"{Request.Scheme}://{Request.Host}";

                var (url, prefId) = await _mp.CreateCheckoutPreferenceAsync(orderId, baseUrl);

                // grava referência se os campos existirem
                var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
                if (order != null)
                {
                    var hasProvider = order.GetType().GetProperty("PaymentProvider") != null;
                    var hasRef = order.GetType().GetProperty("PaymentReference") != null;
                    if (hasProvider) order.GetType().GetProperty("PaymentProvider")!.SetValue(order, "mercadopago");
                    if (hasRef) order.GetType().GetProperty("PaymentReference")!.SetValue(order, prefId);
                    if (hasProvider || hasRef) await _db.SaveChangesAsync();
                }

                return Ok(new { type = "init_point", url, preferenceId = prefId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro ao criar checkout do Mercado Pago para orderId={orderId}", orderId);
                return Problem($"Falha ao iniciar pagamento: {ex.Message}");
            }
        }

        /// <summary>
        /// Navegação em MESMA ABA: redireciona para o init_point do MP.
        /// Front deve chamar window.location.assign(`${API_BASE}/api/payments/mp/go?orderId=...`)
        /// </summary>
        [HttpGet("mp/go")]
        public async Task<IActionResult> Go([FromQuery] int orderId)
        {
            if (orderId <= 0) return BadRequest(new { message = "orderId inválido." });

            try
            {
                var baseUrl = _config["PublicBaseUrl"];
                if (string.IsNullOrWhiteSpace(baseUrl))
                    baseUrl = $"{Request.Scheme}://{Request.Host}";

                var (url, prefId) = await _mp.CreateCheckoutPreferenceAsync(orderId, baseUrl);

                // opcional: salvar provider/ref no pedido
                var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
                if (order != null)
                {
                    var hasProvider = order.GetType().GetProperty("PaymentProvider") != null;
                    var hasRef = order.GetType().GetProperty("PaymentReference") != null;
                    if (hasProvider) order.GetType().GetProperty("PaymentProvider")!.SetValue(order, "mercadopago");
                    if (hasRef) order.GetType().GetProperty("PaymentReference")!.SetValue(order, prefId);
                    if (hasProvider || hasRef) await _db.SaveChangesAsync();
                }

                return Redirect(url);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no mp/go orderId={orderId}", orderId);
                return Problem($"Falha ao iniciar pagamento: {ex.Message}");
            }
        }

        /// <summary>
        /// Retorno do MP após pagamento. Redireciona o cliente de volta ao FRONT na tela da loja,
        /// com parâmetros para o front mostrar o modal de "Pedido Confirmado".
        /// </summary>
        /// GET /api/payments/mp/return/{state}?preference_id=XXX&payment_id=YYY&collection_id=ZZZ
        [HttpGet("mp/return/{state}")]
        public async Task<IActionResult> Return(
            string state,
            [FromQuery(Name = "preference_id")] string? prefId,
            [FromQuery(Name = "payment_id")] string? paymentId,
            [FromQuery(Name = "collection_id")] string? collectionId)
        {
            int? orderId = null;

            try
            {
                // 1) preference_id → external_reference (orderId)
                if (!string.IsNullOrWhiteSpace(prefId))
                    orderId = await _mp.TryGetOrderIdByPreferenceIdAsync(prefId);

                // 2) fallback por payment_id
                if (orderId is null && !string.IsNullOrWhiteSpace(paymentId))
                {
                    var info = await _mp.TryGetPaymentStatusAsync(paymentId);
                    if (info != null && int.TryParse(info.Value.externalReference, out var oid))
                        orderId = oid;
                }

                // 3) fallback por collection_id
                if (orderId is null && !string.IsNullOrWhiteSpace(collectionId))
                {
                    var info = await _mp.TryGetPaymentStatusAsync(collectionId);
                    if (info != null && int.TryParse(info.Value.externalReference, out var oid))
                        orderId = oid;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no retorno do MP (state={state}, pref={prefId})", state, prefId);
            }

            // monta URL do front para voltar à LOJA
            var front = _config["FrontBaseUrl"];
            if (string.IsNullOrWhiteSpace(front))
                front = $"{Request.Scheme}://{Request.Host}";
            var dest = front.TrimEnd('/');
// Tentativa de confirmação imediata: se temos paymentId, checar status e marcar como pago
try
{
    if (orderId is int id2 && !string.IsNullOrWhiteSpace(paymentId))
    {
        var info = await _mp.TryGetPaymentStatusAsync(paymentId);
        if (info != null && info.Value.externalReference == id2.ToString() &&
            string.Equals(info.Value.status, "approved", StringComparison.OrdinalIgnoreCase))
        {
            var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == id2);
            if (order != null && !string.Equals(order.Status, "entregue", StringComparison.OrdinalIgnoreCase))
            {
                order.Status = "pago";
                await _db.SaveChangesAsync();
            }
        }
    }
}
catch (Exception ex)
{
    _logger.LogWarning(ex, "Falha ao confirmar pedido no retorno imediato.");
}

            return orderId is int id
                ? Redirect($"{dest}/?orderId={id}&paid=1")
                : Redirect($"{dest}/?paid=1");
        }

        // Webhook MP
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
                // extrai id
                string? paymentId = null;

                if (Request.Query.TryGetValue("data.id", out var dataId) && !string.IsNullOrWhiteSpace(dataId))
                    paymentId = dataId.ToString();

                if (string.IsNullOrWhiteSpace(paymentId) && Request.Query.TryGetValue("id", out var id))
                    paymentId = id.ToString();

                string? evtType = null;
                if (Request.Query.TryGetValue("type", out var qType) && !string.IsNullOrWhiteSpace(qType)) evtType = qType.ToString();
                if (string.IsNullOrWhiteSpace(evtType) && Request.Query.TryGetValue("topic", out var qTopic) && !string.IsNullOrWhiteSpace(qTopic)) evtType = qTopic.ToString();

                if (string.IsNullOrWhiteSpace(paymentId) || string.IsNullOrWhiteSpace(evtType))
                {
                    if ((Request.ContentLength ?? 0) > 0)
                    {
                        Request.EnableBuffering();
                        using var reader = new StreamReader(Request.Body);
                        var bodyStr = await reader.ReadToEndAsync();
                        Request.Body.Position = 0;

                        if (!string.IsNullOrWhiteSpace(bodyStr))
                        {
                            using var json = System.Text.Json.JsonDocument.Parse(bodyStr);
                            var root = json.RootElement;
                            paymentId ??= root.GetPropertyOrDefault("data")?.GetPropertyOrDefault("id")?.GetString()
                                      ?? root.GetPropertyOrDefault("id")?.GetString();
                            evtType  ??= root.GetPropertyOrDefault("type")?.GetString();
                            _logger.LogInformation("Webhook MP body. type={type}, id={paymentId}", evtType, paymentId);
                        }
                    }
                }

                if (string.IsNullOrWhiteSpace(paymentId))
                {
                    _logger.LogWarning("Webhook MP sem paymentId.");
                    return Ok();
                }

                // resolve status conforme o tipo
                (string status, string externalReference)? statusInfo = null;

                if (!string.IsNullOrWhiteSpace(evtType) &&
                    evtType.Equals("merchant_order", StringComparison.OrdinalIgnoreCase))
                {
                    statusInfo = await _mp.TryGetPaymentStatusByMerchantOrderAsync(paymentId!);
                }
                else
                {
                    statusInfo = await _mp.TryGetPaymentStatusAsync(paymentId!);
                }

                if (statusInfo == null)
                {
                    _logger.LogWarning("Webhook MP: não foi possível resolver status para id={id} tipo={tipo}", paymentId, evtType);
                    return Ok();
                }

                var (status, externalReference) = statusInfo.Value;

                // atualiza pedido
                if (int.TryParse(externalReference, out var orderId))
                {
                    var order = await _db.Orders.FirstOrDefaultAsync(o => o.Id == orderId);
                    if (order != null)
                    {
                        if (string.Equals(status, "approved", StringComparison.OrdinalIgnoreCase))
                        {
                            if (!string.Equals(order.Status, "entregue", StringComparison.OrdinalIgnoreCase))
                            {
                                order.Status = "pago";
                                await _db.SaveChangesAsync();
                                _logger.LogInformation("Order {orderId} -> PAGO (payment {paymentId}).", orderId, paymentId);
                            }
                        }
                        else if (string.Equals(status, "rejected", StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Pagamento REJEITADO order {orderId} (payment {paymentId}).", orderId, paymentId);
                        }
                        else
                        {
                            _logger.LogInformation("Pagamento {paymentId} status {status} (order {orderId}).", paymentId, status, orderId);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("Webhook: order {orderId} não encontrado (payment {paymentId}).", orderId, paymentId);
                    }
                }
                else
                {
                    _logger.LogWarning("Webhook: external_reference inválido: {externalReference}", externalReference);
                }

                return Ok();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Erro no webhook MP.");
                return Ok(); // 200 para evitar retry agressivo
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
