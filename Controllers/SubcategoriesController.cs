using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using e_commerce.Data;
using CSharpAssistant.API.Models;


namespace e_commerce.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class SubcategoriesController : ControllerBase
    {
        private readonly AppDbContext _context;

        public SubcategoriesController(AppDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> GetAll()
        {
            var subcategories = await _context.Subcategories.ToListAsync();
            return Ok(subcategories);
        }

        [HttpPost]
        [Authorize]
        public async Task<IActionResult> Create([FromBody] Subcategory subcategory)
        {
            if (!ModelState.IsValid)
                return BadRequest(ModelState);

            _context.Subcategories.Add(subcategory);
            await _context.SaveChangesAsync();
            return CreatedAtAction(nameof(GetAll), new { id = subcategory.Id }, subcategory);
        }

        [HttpPut("{id}")]
        [Authorize]
        public async Task<IActionResult> Update(int id, [FromBody] Subcategory updated)
        {
            var subcategory = await _context.Subcategories.FindAsync(id);
            if (subcategory == null)
                return NotFound();

            subcategory.Name = updated.Name;
            subcategory.CategoryId = updated.CategoryId;

            await _context.SaveChangesAsync();
            return NoContent();
        }

        [HttpDelete("{id}")]
        [Authorize]
        public async Task<IActionResult> Delete(int id)
        {
            var subcategory = await _context.Subcategories.FindAsync(id);
            if (subcategory == null)
                return NotFound();

            _context.Subcategories.Remove(subcategory);
            await _context.SaveChangesAsync();
            return NoContent();
        }
    }
}
