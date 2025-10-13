using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Data;
using Portfolio.Models;

namespace Portfolio.Pages
{
    public class PostModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public PostModel(ApplicationDbContext db) { _db = db; }
        public BlogPost? Item { get; set; }
        public async Task OnGet(string slug)
        {
            Item = await _db.BlogPosts.FirstOrDefaultAsync(p => p.Slug == slug);
        }
    }
}
