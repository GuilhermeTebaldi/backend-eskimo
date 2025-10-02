using Microsoft.AspNetCore.Mvc;
using CSharpAssistant.API.Data;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace CSharpAssistant.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class StorefrontController : ControllerBase
    {
        private readonly AppDbContext _context;
        public StorefrontController(AppDbContext context) => _context = context;

        public class LayoutItem
        {
            public int? sortRank { get; set; }
            public bool? pinnedTop { get; set; }
        }
        public class LayoutPayload
        {
            public Dictionary<int, LayoutItem> items { get; set; } = new();
        }

        [HttpPut("layout")]
        public async Task<IActionResult> SaveLayout([FromBody] LayoutPayload payload)
        {
            if (payload?.items == null || payload.items.Count == 0)
                return BadRequest("Payload vazio.");

            var ids = payload.items.Keys.ToList();
            var prods = await _context.Products.Where(p => ids.Contains(p.Id)).ToListAsync();

            foreach (var p in prods)
            {
                var i = payload.items[p.Id];
                p.SortRank = i?.sortRank;
                p.PinnedTop = i?.pinnedTop ?? false;
            }

            await _context.SaveChangesAsync();
            return Ok(new { updated = prods.Count });
        }
    }
}
