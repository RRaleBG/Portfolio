using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;
using Portfolio.Models;

namespace Portfolio.Pages.Admin.Blog
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public IndexModel(ApplicationDbContext db) { _db = db; }
        public List<BlogPost> Items { get; set; } = new();
        public async Task OnGet() => Items = await _db.BlogPosts.OrderByDescending(p => p.PublishedAt).ToListAsync();
    }
}
