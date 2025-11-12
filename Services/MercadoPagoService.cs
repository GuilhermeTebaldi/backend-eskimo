// CSharpAssistant.API/Services/MercadoPagoService.cs
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CSharpAssistant.API.Services
{
    /// <summary>
    /// Mercado Pago via REST:
    /// - Cria Preference (Checkout Pro) com itens e frete.
    /// - Consulta pagamento (payment ou merchant_order).
    /// - Resolve orderId a partir de preference_id.
    /// </summary>
    public class MercadoPagoService
    {
        private readonly AppDbContext _db;
        private readonly HttpClient _http;
        private readonly JsonSerializerOptions _jsonOpts;

        public MercadoPagoService(AppDbContext db, IHttpClientFactory httpClientFactory)
        {
            _db = db;
            _http = httpClientFactory.CreateClient(nameof(MercadoPagoService));
            _http.Timeout = TimeSpan.FromSeconds(30);

            _jsonOpts = new JsonSerializerOptions
            {
                PropertyNamingPolicy = null,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };
        }

        public async Task<(string initPointUrl, string preferenceId)> CreateCheckoutPreferenceAsync(
            int orderId,
            string publicBaseUrl,
            CancellationToken ct = default)
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order == null) throw new InvalidOperationException("Pedido não encontrado.");
            if (order.Items == null || !order.Items.Any()) throw new InvalidOperationException("Pedido sem itens.");
            if (string.IsNullOrWhiteSpace(order.Store)) throw new InvalidOperationException("Pedido sem loja definida.");

            // config da LOJA
            var storeSlug = (order.Store ?? "").Trim().ToLower();
            var config = await _db.Set<PaymentConfig>()
                .AsNoTracking()
                .Where(c =>
                    (c.Store ?? "").Trim().ToLower() == storeSlug &&
                    c.IsActive &&
                    c.Provider.ToLower() == "mercadopago" &&
                    !string.IsNullOrWhiteSpace(c.MpAccessToken))
                .OrderByDescending(c => c.UpdatedAt) // ou Id
                .FirstOrDefaultAsync(ct);

            if (config == null) throw new InvalidOperationException($"Loja '{order.Store}' sem configuração ativa do Mercado Pago.");
            if (string.IsNullOrWhiteSpace(config.MpAccessToken)) throw new InvalidOperationException($"Loja '{order.Store}' sem Access Token do Mercado Pago.");

            // entrega
            decimal itemsSum = order.Items.Sum(i => Math.Round((decimal)i.Price, 2, MidpointRounding.AwayFromZero) * i.Quantity);
            decimal deliveryFee = 0m;
            try
            {
                var dfProp = order.GetType().GetProperty("DeliveryFee");
                if (dfProp != null)
                {
                    var val = dfProp.GetValue(order);
                    if (val is decimal d) deliveryFee = Math.Round(d, 2, MidpointRounding.AwayFromZero);
                    else if (val is double dd) deliveryFee = Math.Round((decimal)dd, 2, MidpointRounding.AwayFromZero);
                    else if (val is float ff) deliveryFee = Math.Round((decimal)ff, 2, MidpointRounding.AwayFromZero);
                }
            }
            catch { }
            if (deliveryFee <= 0m)
            {
                try
                {
                    var totalProp = order.GetType().GetProperty("Total");
                    if (totalProp != null)
                    {
                        decimal totalVal = 0m;
                        var val = totalProp.GetValue(order);
                        if (val is decimal d) totalVal = d;
                        else if (val is double dd) totalVal = (decimal)dd;
                        else if (val is float ff) totalVal = (decimal)ff;
                        var diff = totalVal - itemsSum;
                        if (diff > 0m) deliveryFee = Math.Round(diff, 2, MidpointRounding.AwayFromZero);
                    }
                }
                catch { }
            }

            var items = order.Items.Select(i => new
            {
                title = i.Name,
                unit_price = Math.Round((decimal)i.Price, 2, MidpointRounding.AwayFromZero),
                quantity = i.Quantity,
                currency_id = "BRL",
                picture_url = string.IsNullOrWhiteSpace(i.ImageUrl) ? null : i.ImageUrl
            }).ToArray();

            object? shipments = null;
            if (deliveryFee > 0m)
            {
                shipments = new { cost = deliveryFee, mode = "not_specified" };
            }

           var preference = new
{
    items,
    payer = new { name = order.CustomerName },
    external_reference = order.Id.ToString(),
    back_urls = new
    {
        success = $"{publicBaseUrl}/api/payments/mp/return/success",
        failure = $"{publicBaseUrl}/api/payments/mp/return/failure",
        pending = $"{publicBaseUrl}/api/payments/mp/return/pending"
    },
    auto_return = "approved",
    notification_url = $"{publicBaseUrl}/api/payments/mp/webhook",
    shipments,
    // Ajuda a não ficar em "pending" intermediário em cartão; no PIX ele vira approved ao pagar
    binary_mode = true,
    payment_methods = new
    {
        // não excluímos 'credit_card' nem 'ticket', deixamos tudo habilitado
        installments = 12,
        default_installments = 1
    },
    // Melhora a identificação no extrato do cliente
    statement_descriptor = "ESKIMO-CHAPECO"
};


            var json = JsonSerializer.Serialize(preference, _jsonOpts);

            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/checkout/preferences");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.MpAccessToken);
            req.Headers.Accept.ParseAdd("application/json");
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
                throw new InvalidOperationException($"Mercado Pago error: {resp.StatusCode} - {body}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;

            var initPoint = root.GetPropertyOrDefault("init_point")?.GetString()
                            ?? root.GetPropertyOrDefault("sandbox_init_point")?.GetString();
            var prefId = root.GetPropertyOrDefault("id")?.GetString();

            if (string.IsNullOrWhiteSpace(initPoint) || string.IsNullOrWhiteSpace(prefId))
                throw new InvalidOperationException("Resposta do Mercado Pago inválida (sem init_point ou id).");

            return (initPoint!, prefId!);
        }

        public async Task<(string status, string externalReference)?> TryGetPaymentStatusAsync(string paymentId, CancellationToken ct = default)
        {
            var tokens = await _db.Set<PaymentConfig>()
                .AsNoTracking()
                .Where(c => c.IsActive && c.Provider.ToLower() == "mercadopago" && c.MpAccessToken != null)
                .Select(c => c.MpAccessToken!)
                .Distinct()
                .ToListAsync(ct);

            foreach (var token in tokens)
            {
                try
                {
                    var res = await GetPaymentRawAsync(paymentId, token, ct);
                    try
                    {
                        if (!res.isOk) continue;
                        var status = res.body.RootElement.GetPropertyOrDefault("status")?.GetString() ?? "";
                        var extRef = res.body.RootElement.GetPropertyOrDefault("external_reference")?.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(status))
                            return (status, extRef);
                    }
                    finally { res.body.Dispose(); }
                }
                catch { }
            }
            return null;
        }

        public async Task<(string status, string externalReference)?> TryGetPaymentStatusByMerchantOrderAsync(string merchantOrderId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(merchantOrderId)) return null;

            var tokens = await _db.Set<PaymentConfig>()
                .AsNoTracking()
                .Where(c => c.IsActive && c.Provider.ToLower() == "mercadopago" && c.MpAccessToken != null)
                .Select(c => c.MpAccessToken!)
                .Distinct()
                .ToListAsync(ct);

            foreach (var token in tokens)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.mercadopago.com/merchant_orders/{merchantOrderId}");
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    req.Headers.Accept.ParseAdd("application/json");

                    using var resp = await _http.SendAsync(req, ct);
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    if (!resp.IsSuccessStatusCode) continue;

                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;

                    string? paymentId = null;
                    if (root.TryGetProperty("payments", out var payments) && payments.ValueKind == JsonValueKind.Array)
                    {
                        string? approvedId = null;
                        string? anyId = null;
                        foreach (var p in payments.EnumerateArray())
                        {
                            var pid = p.GetPropertyOrDefault("id")?.GetInt64().ToString();
                            var pstatus = p.GetPropertyOrDefault("status")?.GetString();
                            if (!string.IsNullOrWhiteSpace(pid))
                            {
                                anyId ??= pid;
                                if (string.Equals(pstatus, "approved", StringComparison.OrdinalIgnoreCase))
                                {
                                    approvedId = pid;
                                    break;
                                }
                            }
                        }
                        paymentId = approvedId ?? anyId;
                    }

                    if (string.IsNullOrWhiteSpace(paymentId)) continue;

                    var res = await GetPaymentRawAsync(paymentId!, token, ct);
                    try
                    {
                        if (!res.isOk) continue;
                        var status = res.body.RootElement.GetPropertyOrDefault("status")?.GetString() ?? "";
                        var extRef = res.body.RootElement.GetPropertyOrDefault("external_reference")?.GetString() ?? "";
                        if (!string.IsNullOrWhiteSpace(status))
                            return (status, extRef);
                    }
                    finally { res.body.Dispose(); }
                }
                catch { }
            }
            return null;
        }

        public async Task<(string status, string externalReference, string? paymentId)?> TryGetPaymentStatusByExternalReferenceAsync(int orderId, CancellationToken ct = default)
        {
            var externalReference = orderId.ToString();

            var tokens = await _db.Set<PaymentConfig>()
                .AsNoTracking()
                .Where(c => c.IsActive && c.Provider.ToLower() == "mercadopago" && !string.IsNullOrWhiteSpace(c.MpAccessToken))
                .Select(c => c.MpAccessToken!)
                .Distinct()
                .ToListAsync(ct);

            foreach (var token in tokens)
            {
                try
                {
                    var url =
                        $"https://api.mercadopago.com/v1/payments/search?external_reference={Uri.EscapeDataString(externalReference)}&sort=date_created&criteria=desc&limit=5";

                    using var req = new HttpRequestMessage(HttpMethod.Get, url);
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    req.Headers.Accept.ParseAdd("application/json");

                    using var resp = await _http.SendAsync(req, ct);
                    if (!resp.IsSuccessStatusCode) continue;

                    var body = await resp.Content.ReadAsStringAsync(ct);
                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    var results = root.GetPropertyOrDefault("results");
                    if (results == null || results.Value.ValueKind != JsonValueKind.Array)
                        continue;

                    foreach (var result in results.Value.EnumerateArray())
                    {
                        var status = result.GetPropertyOrDefault("status")?.GetString() ?? "";
                        var extRef = result.GetPropertyOrDefault("external_reference")?.GetString() ?? "";
                        var paymentId = result.GetPropertyOrDefault("id")?.GetInt64().ToString();

                        if (!string.IsNullOrWhiteSpace(status) && !string.IsNullOrWhiteSpace(extRef))
                            return (status, extRef, paymentId);
                    }
                }
                catch
                {
                    // ignore and try next token
                }
            }

            return null;
        }

        public async Task<int?> TryGetOrderIdByPreferenceIdAsync(string prefId, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(prefId)) return null;

            var tokens = await _db.Set<PaymentConfig>()
                .AsNoTracking()
                .Where(c => c.IsActive && c.Provider.ToLower() == "mercadopago" && !string.IsNullOrWhiteSpace(c.MpAccessToken))
                .Select(c => c.MpAccessToken!)
                .Distinct()
                .ToListAsync(ct);

            foreach (var token in tokens)
            {
                try
                {
                    using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.mercadopago.com/checkout/preferences/{prefId}");
                    req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                    req.Headers.Accept.ParseAdd("application/json");

                    using var resp = await _http.SendAsync(req, ct);
                    var body = await resp.Content.ReadAsStringAsync(ct);
                    if (!resp.IsSuccessStatusCode) continue;

                    using var doc = JsonDocument.Parse(body);
                    var root = doc.RootElement;
                    var extRef = root.GetPropertyOrDefault("external_reference")?.GetString();

                    if (int.TryParse(extRef, out var oid)) return oid;
                }
                catch { }
            }
            return null;
        }

        private async Task<(bool isOk, JsonDocument body)> GetPaymentRawAsync(string paymentId, string accessToken, CancellationToken ct)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.mercadopago.com/v1/payments/{paymentId}");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            req.Headers.Accept.ParseAdd("application/json");

            using var resp = await _http.SendAsync(req, ct);
            var bodyStr = await resp.Content.ReadAsStringAsync(ct);
            var json = JsonDocument.Parse(bodyStr);
            return (resp.IsSuccessStatusCode, json);
        }
    }

    internal static class JsonExt
    {
        public static JsonElement? GetPropertyOrDefault(this JsonElement element, string name)
        {
            if (element.ValueKind != JsonValueKind.Object) return null;
            if (element.TryGetProperty(name, out var value)) return value;
            return null;
        }
    }
}
