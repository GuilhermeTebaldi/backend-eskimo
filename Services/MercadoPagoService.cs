// CSharpAssistant.API/Services/MercadoPagoService.cs
using System;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using Microsoft.EntityFrameworkCore;

namespace CSharpAssistant.API.Services
{
    /// <summary>
    /// Serviço mínimo para criar Preference (checkout) no Mercado Pago e consultar pagamentos.
    /// - NÃO usa SDK; faz chamadas REST com HttpClient.
    /// - Usa PaymentConfig por loja para pegar MpAccessToken correto (uma conta por CNPJ).
    /// </summary>
    public class MercadoPagoService
    {
        private static readonly HttpClient _http = new HttpClient();
        private readonly AppDbContext _db;

        public MercadoPagoService(AppDbContext db)
        {
            _db = db;
        }

        /// <summary>
        /// Cria uma Preference de checkout no Mercado Pago para o pedido informado.
        /// Retorna (initPointUrl, preferenceId).
        /// </summary>
        public async Task<(string initPointUrl, string preferenceId)> CreateCheckoutPreferenceAsync(
            int orderId,
            string requestBaseUrl // ex: https://backend-eskimo.onrender.com
        )
        {
            var order = await _db.Orders
                .Include(o => o.Items)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
                throw new InvalidOperationException("Pedido não encontrado.");

            if (order.Items == null || !order.Items.Any())
                throw new InvalidOperationException("Pedido sem itens.");

            if (string.IsNullOrWhiteSpace(order.Store))
                throw new InvalidOperationException("Pedido sem loja definida.");

            // Busca a configuração de pagamento por loja (case-insensitive)
            var config = await _db.Set<PaymentConfig>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c => c.Store.ToLower() == order.Store.ToLower());

            if (config == null || !config.IsActive || config.Provider?.ToLower() != "mercadopago")
                throw new InvalidOperationException($"Loja '{order.Store}' sem configuração ativa do Mercado Pago.");

            if (string.IsNullOrWhiteSpace(config.MpAccessToken))
                throw new InvalidOperationException($"Loja '{order.Store}' sem Access Token do Mercado Pago.");

            // Monta payload da Preference
            var preference = new
            {
                items = order.Items.Select(i => new
                {
                    title = i.Name,
                    unit_price = (decimal)i.Price,
                    quantity = i.Quantity,
                    currency_id = "BRL",
                    picture_url = string.IsNullOrWhiteSpace(i.ImageUrl) ? null : i.ImageUrl
                }).ToArray(),
                payer = new
                {
                    name = order.CustomerName
                },
                external_reference = order.Id.ToString(), // usaremos no webhook
                back_urls = new
                {
                    success = $"{requestBaseUrl}/payments/success",
                    failure = $"{requestBaseUrl}/payments/failure",
                    pending = $"{requestBaseUrl}/payments/pending"
                },
                auto_return = "approved",
                notification_url = $"{requestBaseUrl}/api/payments/mp/webhook", // webhook da sua API
                payment_methods = new
                {
                    installments = 12, // máximo de parcelas
                    default_installments = 1
                }
            };

            var json = JsonSerializer.Serialize(preference, new JsonSerializerOptions { IgnoreNullValues = true });
            using var req = new HttpRequestMessage(HttpMethod.Post, "https://api.mercadopago.com/checkout/preferences");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", config.MpAccessToken);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using var resp = await _http.SendAsync(req);
            var body = await resp.Content.ReadAsStringAsync();

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
        /// Consulta um pagamento no Mercado Pago pelo ID e retorna um objeto com status e external_reference.
        /// OBS: Como cada CNPJ tem um AccessToken, tentamos em todas as configs ativas até achar.
        /// </summary>
        public async Task<(string status, string externalReference)?> TryGetPaymentStatusAsync(string paymentId)
        {
            var tokens = await _db.Set<PaymentConfig>()
                .AsNoTracking()
                .Where(c => c.IsActive && c.Provider.ToLower() == "mercadopago" && c.MpAccessToken != null)
                .Select(c => c.MpAccessToken!)
                .Distinct()
                .ToListAsync();

            foreach (var token in tokens)
            {
                try
                {
                    var res = await GetPaymentRawAsync(paymentId, token);
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

        private async Task<(bool isOk, JsonDocument body)> GetPaymentRawAsync(string paymentId, string accessToken)
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"https://api.mercadopago.com/v1/payments/{paymentId}");
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

            using var resp = await _http.SendAsync(req);
            var bodyStr = await resp.Content.ReadAsStringAsync();
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
