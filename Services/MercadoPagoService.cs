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
using Microsoft.Extensions.Configuration;

namespace CSharpAssistant.API.Services
{
    /// <summary>
    /// Serviço para criar Preference (checkout) no Mercado Pago e consultar pagamentos (REST, sem SDK).
    /// - Usa PaymentConfig por loja para pegar o MpAccessToken correto (uma conta por loja).
    /// - External_reference = order.Id (vincula pagamento ao pedido).
    /// - back_urls + notification_url (webhook) configurados com base na URL pública.
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
                PropertyNamingPolicy = null, // MP usa snake_case/sem padrão fixo na resposta
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
                WriteIndented = false
            };
        }

        /// <summary>
        /// Cria uma Preference de checkout no Mercado Pago para o pedido informado.
        /// Retorna (initPointUrl, preferenceId).
        /// </summary>
        public async Task<(string initPointUrl, string preferenceId)> CreateCheckoutPreferenceAsync(
            int orderId,
            string publicBaseUrl, // ex: https://backend-eskimo.onrender.com (ou URL pública setada)
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

            // 1) Busca a configuração de pagamento por loja (case-insensitive)
            var config = await _db.Set<PaymentConfig>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Store.ToLower() == order.Store.ToLower(), ct);

            if (config == null || !config.IsActive || string.IsNullOrWhiteSpace(config.Provider) || config.Provider.ToLower() != "mercadopago")
                throw new InvalidOperationException($"Loja '{order.Store}' sem configuração ativa do Mercado Pago.");

            if (string.IsNullOrWhiteSpace(config.MpAccessToken))
                throw new InvalidOperationException($"Loja '{order.Store}' sem Access Token do Mercado Pago.");

            // 2) Monta payload da Preference
            var preference = new
            {
                items = order.Items.Select(i => new
                {
                    title = i.Name,
                    unit_price = Math.Round((decimal)i.Price, 2, MidpointRounding.AwayFromZero),
                    quantity = i.Quantity,
                    currency_id = "BRL",
                    picture_url = string.IsNullOrWhiteSpace(i.ImageUrl) ? null : i.ImageUrl
                }).ToArray(),

                payer = new
                {
                    name = order.CustomerName
                },

                external_reference = order.Id.ToString(), // usado no webhook p/ localizar o pedido

                back_urls = new
                {
                    success = $"{publicBaseUrl}/payments/success",
                    failure = $"{publicBaseUrl}/payments/failure",
                    pending  = $"{publicBaseUrl}/payments/pending"
                },

                auto_return = "approved",

                // Webhook da sua API (server-to-server)
                notification_url = $"{publicBaseUrl}/api/payments/mp/webhook",

                // (Opcional) regras de pagamento
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

            return (initPoint, prefId);
        }

        /// <summary>
        /// Consulta um pagamento no Mercado Pago pelo ID e retorna (status, external_reference).
        /// Tenta em todas as contas ativas (todas as lojas com provider=mercadopago).
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
