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

        [HttpGet("{store}")]
        public async Task<IActionResult> GenerateReport(string store)
        {
            var storeKey = store.ToLower().Trim().Replace("-", "").Replace(" ", "");

            string storeName;
            if (storeKey.Contains("passo")) storeName = "passo";
            else if (storeKey.Contains("efapi")) storeName = "efapi";
            else if (storeKey.Contains("palmital")) storeName = "palmital";
            else storeName = storeKey;

            var pedidos = await _context.Orders
                .Include(p => p.Items)
                .Where(p => p.Store.ToLower() == storeName)
                .OrderByDescending(p => p.Id)
                .ToListAsync();

            if (!pedidos.Any())
                return NotFound(new { message = $"Nenhum pedido encontrado para a loja {storeName}." });

            var totalGeral = pedidos.Sum(p => p.Total);
            var pdfStream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header()
                        .Text($"Relatório de Pedidos - Loja {storeName.ToUpper()}")
                        .SemiBold().FontSize(18).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(10)
                        .Column(col =>
                        {
                            foreach (var pedido in pedidos)
                            {
                                var dataPedido = pedido.CreatedAt.ToLocalTime();
                                var dataFormatada = dataPedido.ToString("dd/MM/yyyy");
                                var horaFormatada = dataPedido.ToString("HH:mm");

                                col.Item().BorderBottom(1).Padding(5).Column(innerCol =>
                                {
                                    innerCol.Item().Text($"Cliente: {pedido.CustomerName}");
                                    innerCol.Item().Text($"Data: {dataFormatada}  Horário: {horaFormatada}");
                                    innerCol.Item().Text($"Total: R$ {pedido.Total:F2}");
                                    innerCol.Item().Text($"Status: {pedido.Status.ToUpper()}");
                                    innerCol.Item().PaddingTop(5);

                                    foreach (var item in pedido.Items)
                                    {
                                        innerCol.Item().Text($" - {item.Name} (x{item.Quantity})  R$ {(item.Price * item.Quantity):F2}")
                                            .FontSize(10).FontColor(Colors.Grey.Darken2);
                                    }
                                });
                            }

                            col.Item().PaddingTop(20).AlignRight().Text($"Total Geral: R$ {totalGeral:F2}")
                                .Bold().FontSize(14).FontColor(Colors.Black);
                        });

                    page.Footer()
                        .AlignCenter()
                        .Text($"Gerado em {System.DateTime.Now:dd/MM/yyyy HH:mm}")
                        .FontSize(10).FontColor(Colors.Grey.Medium);
                });
            })
            .GeneratePdf(pdfStream);

            pdfStream.Position = 0;
            var fileName = $"relatorio_{storeName}.pdf";
            return File(pdfStream, "application/pdf", fileName);
        }
    }
}
