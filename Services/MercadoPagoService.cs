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
    /// Serviço Mercado Pago (REST, sem SDK):
    /// - Cria Preference (Checkout Pro) com itens do pedido + taxa de entrega (shipments.cost).
    /// - Consulta pagamento pelo ID, retornando (status, external_reference).
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
                PropertyNamingPolicy = null, // manter chaves como enviamos
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Cria uma Preference no Mercado Pago para o pedido informado.
        /// Retorna (initPointUrl, preferenceId).
        /// </summary>
        public async Task<(string initPointUrl, string preferenceId)> CreateCheckoutPreferenceAsync(
            int orderId,
            string publicBaseUrl, // ex.: https://backend-eskimo.onrender.com
            CancellationToken ct = default
        )
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId, ct);

            if (order == null)
                throw new InvalidOperationException("Pedido não encontrado.");

            if (order.Items == null || !order.Items.Any())
                throw new InvalidOperationException("Pedido sem itens.");

            if (string.IsNullOrWhiteSpace(order.Store))
                throw new InvalidOperationException("Pedido sem loja definida.");

            // 1) Config da loja (credenciais Mercado Pago)
            var storeSlug = (order.Store ?? "").Trim().ToLower();

var config = await _db.Set<PaymentConfig>()
    .AsNoTracking()
    .Where(c =>
        (c.Store ?? "").Trim().ToLower() == storeSlug &&
        c.IsActive &&
        c.Provider.ToLower() == "mercadopago" &&
        !string.IsNullOrWhiteSpace(c.MpAccessToken))
    .OrderByDescending(c => c.UpdatedAt)   // se não existir UpdatedAt, use .OrderByDescending(c => c.Id)
    .FirstOrDefaultAsync(ct);


            if (config == null || !config.IsActive || string.IsNullOrWhiteSpace(config.Provider) || config.Provider.ToLower() != "mercadopago")
                throw new InvalidOperationException($"Loja '{order.Store}' sem configuração ativa do Mercado Pago.");

            if (string.IsNullOrWhiteSpace(config.MpAccessToken))
                throw new InvalidOperationException($"Loja '{order.Store}' sem Access Token do Mercado Pago.");

            // 2) Soma dos itens (centavos arredondados corretamente)
            decimal itemsSum = order.Items.Sum(i =>
                Math.Round((decimal)i.Price, 2, MidpointRounding.AwayFromZero) * i.Quantity);

            // 3) Calcula taxa de entrega de forma resiliente:
            decimal deliveryFee = 0m;

            // 3a) se existir a propriedade DeliveryFee no modelo, usa ela
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
            catch { /* ignora e tenta calcular pela diferença */ }

            // 3b) se não vier explicitamente, tenta inferir por (Total - itens)
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
                catch { /* noop */ }
            }

            // 4) Monta payload da Preference (inclui shipments.cost quando houver entrega)
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
                shipments = new
                {
                    cost = deliveryFee,
                    mode = "not_specified" // frete customizado
                };
            }

            var preference = new
            {
                items,
                payer = new
                {
                    name = order.CustomerName
                },

                external_reference = order.Id.ToString(), // usado no webhook pra localizar o pedido

                back_urls = new
                {
                    success = $"{publicBaseUrl}/payments/success",
                    failure = $"{publicBaseUrl}/payments/failure",
                    pending = $"{publicBaseUrl}/payments/pending"
                },

                auto_return = "approved",

                // Webhook da sua API (server-to-server)
                notification_url = $"{publicBaseUrl}/api/payments/mp/webhook",

                // Frete/entrega (só envia se > 0)
                shipments,

                // (opcional) regras de parcelamento
                payment_methods = new
                {
                    installments = 12,
                    default_installments = 1
                }
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

        /// <summary>
        /// Consulta um pagamento e retorna (status, external_reference) ou null.
        /// Tenta todos os tokens ativos (todas as lojas provider=mercadopago).
        /// </summary>
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
                    if (res.isOk)
                    {
                        var status = res.body.RootElement.GetPropertyOrDefault("status")?.GetString() ?? "";
                        var extRef = res.body.RootElement.GetPropertyOrDefault("external_reference")?.GetString() ?? "";
                        res.body.Dispose();
                        return (status, extRef);
                    }
                    res.body.Dispose();
                }
                catch
                {
                    // tenta próximo token
                }
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
