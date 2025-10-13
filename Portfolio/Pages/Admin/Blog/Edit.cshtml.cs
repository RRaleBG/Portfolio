using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Data;
using Portfolio.Models;

namespace Portfolio.Pages.Admin.Blog
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public EditModel(ApplicationDbContext db) { _db = db; }
        [BindProperty]
        public BlogPost Item { get; set; } = new();
        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _db.BlogPosts.FindAsync(id);
            if (item == null) return NotFound();
            Item = item; return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            _db.Attach(Item).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
