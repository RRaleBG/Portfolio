using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Data;
using Portfolio.Models;

namespace Portfolio.Pages.Admin.Blog
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public CreateModel(ApplicationDbContext db) { _db = db; }
        [BindProperty]
        public BlogPost Item { get; set; } = new();
        public void OnGet() { }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            Item.PublishedAt = DateTime.UtcNow;
            _db.BlogPosts.Add(Item);
            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
