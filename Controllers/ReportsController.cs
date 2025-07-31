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
            // 🔥 Normaliza a URL
    var storeKey = store.ToLower().Replace("-", "").Replace(" ", "");

    string storeName;
    if (storeKey == "passodosfortes" || storeKey == "passo") storeName = "passo";
    else if (storeKey == "efapi") storeName = "efapi";
    else if (storeKey == "palmital") storeName = "palmital";
    else storeName = storeKey;

            // 🔥 Garante comparação case-insensitive no banco
           var pedidos = await _context.Orders
        .Include(p => p.Items)
        .Where(p => p.Store.ToLower() == storeName)
        .OrderByDescending(p => p.Id)
        .ToListAsync();

    if (!pedidos.Any())
        return NotFound(new { message = "Nenhum pedido encontrado para esta loja." });


            var totalGeral = pedidos.Sum(p => p.Total);
            var pdfStream = new MemoryStream();

            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Header()
                        .Text($"Relatório de Pedidos - Loja {storeName}")
                        .SemiBold().FontSize(18).FontColor(Colors.Blue.Medium);

                    page.Content()
                        .PaddingVertical(10)
                        .Column(col =>
                        {
                            foreach (var pedido in pedidos)
                            {
                                col.Item().BorderBottom(1).Padding(5).Row(row =>
                                {
                                    row.RelativeColumn().Text($"Cliente: {pedido.CustomerName}");
                                    row.RelativeColumn().Text($"Total: R$ {pedido.Total:F2}");
                                    row.RelativeColumn().Text($"Status: {pedido.Status.ToUpper()}");
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
            var fileName = $"relatorio_{storeKey}.pdf";
            return File(pdfStream, "application/pdf", fileName);
        }
    }
}
