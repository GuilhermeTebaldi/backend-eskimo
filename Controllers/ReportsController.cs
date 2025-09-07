using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CSharpAssistant.API.Data;
using CSharpAssistant.API.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System;
using System.Collections.Generic;
using System.Globalization;

namespace CSharpAssistant.API.Models
{
    [ApiController]
    [Route("api/[controller]")]
    public class ReportsController : ControllerBase
    {
        private readonly AppDbContext _context;

        public ReportsController(AppDbContext context)
        {
            _context = context;
        }

        /// <summary>
        /// Gera relatório de pedidos por loja.
        /// Filtros opcionais por data (UTC): ?from=YYYY-MM-DD&to=YYYY-MM-DD
        /// Regras:
        /// - Apenas pedidos com Status = "pago" ou "entregue" entram no relatório.
        /// - Pedidos "cancelado" e "pendente" são ignorados.
        /// - Agrupamento por dia é feito em horário local do Brasil (-03:00), seguindo o padrão que você já usava.
        /// </summary>
        [HttpGet("{store}")]
        public async Task<IActionResult> GenerateReport(string store, [FromQuery] string? from = null, [FromQuery] string? to = null)
        {
            // Normaliza store
            var storeKey = (store ?? string.Empty).ToLower().Trim().Replace("-", "").Replace(" ", "");
            string storeName;
            if (storeKey.Contains("passo")) storeName = "passo";
            else if (storeKey.Contains("efapi")) storeName = "efapi";
            else if (storeKey.Contains("palmital")) storeName = "palmital";
            else storeName = storeKey;

            // Constrói query base
            var query = _context.Orders
                .Include(p => p.Items)
                .Where(p => p.Store.ToLower() == storeName)
                // 🚫 Ignora "cancelado" e "pendente"
                .Where(p => p.Status == "pago" || p.Status == "entregue")
                .AsQueryable();

            // Filtros de data em UTC (from <= CreatedAt < to+1)
            if (TryParseDate(from, out var fromDateUtc))
            {
                query = query.Where(p => p.CreatedAt >= fromDateUtc.Value);
            }
            if (TryParseDate(to, out var toDateUtc))
            {
                var toExclusive = toDateUtc.Value.AddDays(1); // exclusivo
                query = query.Where(p => p.CreatedAt < toExclusive);
            }

            var pedidos = await query
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            if (!pedidos.Any())
                return NotFound(new { message = $"Nenhum pedido encontrado para a loja {storeName} com os filtros aplicados." });

            // 🔒 Filtrar apenas pedidos com data válida (defensivo)
            var pedidosValidos = pedidos.Where(p => p.CreatedAt > DateTime.MinValue).ToList();

            // Ajuste simples de fuso (mesma lógica que você já usava)
            DateTime ToLocalBr(DateTime dtUtc) => dtUtc.ToUniversalTime().AddHours(-3);

            // 🔥 Agrupar por dia (dd/MM/yyyy) usando fuso "local" (BR -3h)
            var pedidosPorDia = pedidosValidos
                .GroupBy(p => ToLocalBr(p.CreatedAt).ToString("dd/MM/yyyy"))
                .OrderByDescending(g => ParseBrDate(g.Key))
                .ToList();

            var totalGeral = pedidosValidos.Sum(p => p.Total);
            var pdfStream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header()
                        .Text(header =>
                        {
                            header.Span($"Relatório de Pedidos - Loja {storeName.ToUpper()}").SemiBold().FontSize(18).FontColor(Colors.Blue.Medium);
                            if (fromDateUtc.HasValue || toDateUtc.HasValue)
                            {
                                header.Line("");
                                header.Span(BuildRangeText(fromDateUtc, toDateUtc)).FontSize(10).FontColor(Colors.Grey.Darken2);
                            }
                        });

                    page.Content()
                        .PaddingVertical(10)
                        .Column(col =>
                        {
                            foreach (var grupo in pedidosPorDia)
                            {
                                // Título do dia
                                col.Item().PaddingBottom(5)
                                   .Text($"📅 {grupo.Key}")
                                   .Bold().FontSize(14).FontColor(Colors.Black);

                                // Pedidos do dia
                                foreach (var pedido in grupo)
                                {
                                    var dataLocal = ToLocalBr(pedido.CreatedAt);
                                    var horaFormatada = dataLocal.ToString("HH:mm");

                                    col.Item().BorderBottom(1).Padding(5).Column(innerCol =>
                                    {
                                        innerCol.Item().Text($"Cliente: {pedido.CustomerName}");
                                        innerCol.Item().Text($"Horário: {horaFormatada}");
                                        innerCol.Item().Text($"Total: R$ {pedido.Total:F2}");
                                        innerCol.Item().Text($"Status: {pedido.Status.ToUpper()}");

                                        foreach (var item in pedido.Items)
                                        {
                                            innerCol.Item()
                                                .Text($" - {item.Name} (x{item.Quantity})  R$ {(item.Price * item.Quantity):F2}")
                                                .FontSize(10).FontColor(Colors.Grey.Darken2);
                                        }
                                    });
                                }

                                // Subtotal do dia
                                var totalDia = grupo.Sum(p => p.Total);
                                col.Item().PaddingTop(5).AlignRight()
                                    .Text($"Total do dia {grupo.Key}: R$ {totalDia:F2}")
                                    .FontSize(12).Bold().FontColor(Colors.Black);

                                col.Item().PaddingTop(10);
                            }

                            // Total geral
                            col.Item().PaddingTop(20).AlignRight().Text($"TOTAL GERAL: R$ {totalGeral:F2}")
                                .Bold().FontSize(14).FontColor(Colors.Black);
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text($"Gerado em {ToLocalBr(DateTime.UtcNow):dd/MM/yyyy HH:mm}")
                        .FontSize(10).FontColor(Colors.Grey.Medium);
                });
            })
            .GeneratePdf(pdfStream);

            pdfStream.Position = 0;

            // Nome do arquivo com loja e, se houver, o range de datas
            var suffix = BuildFileSuffix(fromDateUtc, toDateUtc);
            var fileName = $"relatorio_{storeName}{suffix}.pdf";

            return File(pdfStream, "application/pdf", fileName);
        }

        // ===== Helpers =====

        private static bool TryParseDate(string? s, out DateTime? resultUtc)
        {
            resultUtc = null;
            if (string.IsNullOrWhiteSpace(s)) return false;

            // Aceita "YYYY-MM-DD" (UTC, meia-noite)
            if (DateTime.TryParseExact(s.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                       DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                       out var parsed))
            {
                // Normaliza para 00:00:00 UTC
                resultUtc = new DateTime(parsed.Year, parsed.Month, parsed.Day, 0, 0, 0, DateTimeKind.Utc);
                return true;
            }

            // Tentativa bruta como fallback
            if (DateTime.TryParse(s, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out parsed))
            {
                resultUtc = new DateTime(parsed.Year, parsed.Month, parsed.Day, 0, 0, 0, DateTimeKind.Utc);
                return true;
            }

            return false;
        }

        private static DateTime ParseBrDate(string ddMMyyyy)
        {
            // "dd/MM/yyyy" -> DateTime
            return DateTime.ParseExact(ddMMyyyy, "dd/MM/yyyy", CultureInfo.InvariantCulture);
        }

        private static string BuildRangeText(DateTime? fromUtc, DateTime? toUtc)
        {
            if (fromUtc.HasValue && toUtc.HasValue)
                return $"Período: {fromUtc.Value:dd/MM/yyyy} a {toUtc.Value:dd/MM/yyyy}";
            if (fromUtc.HasValue)
                return $"A partir de: {fromUtc.Value:dd/MM/yyyy}";
            if (toUtc.HasValue)
                return $"Até: {toUtc.Value:dd/MM/yyyy}";
            return string.Empty;
        }

        private static string BuildFileSuffix(DateTime? fromUtc, DateTime? toUtc)
        {
            if (fromUtc.HasValue && toUtc.HasValue)
                return $"_{fromUtc.Value:yyyyMMdd}-{toUtc.Value:yyyyMMdd}";
            if (fromUtc.HasValue)
                return $"_{fromUtc.Value:yyyyMMdd}-";
            if (toUtc.HasValue)
                return $"_-{toUtc.Value:yyyyMMdd}";
            return "";
        }
    }
}
