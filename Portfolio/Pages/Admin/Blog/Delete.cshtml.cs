using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Data;
using Portfolio.Models;

namespace Portfolio.Pages.Admin.Blog
{
    [Authorize]
    public class DeleteModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public DeleteModel(ApplicationDbContext db) { _db = db; }
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
            var e = await _db.BlogPosts.FindAsync(Item.Id);
            if (e != null){ _db.BlogPosts.Remove(e); await _db.SaveChangesAsync(); }
            return RedirectToPage("Index");
        }
    }
}
