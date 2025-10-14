using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;
using Portfolio.Models;
using Microsoft.AspNetCore.Http;

namespace Portfolio.Pages
{
    public class PostModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public PostModel(ApplicationDbContext db) { _db = db; }
        public BlogPost? Item { get; set; }

        public async Task OnGetAsync(string slug)
        {
            await LogVisitAsync("Post");
            Item = await _db.BlogPosts.FirstOrDefaultAsync(p => p.Slug == slug);
        }

        public async Task LogVisitAsync(string pageName)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _db.PageVisits.Add(new PageVisit { Page = pageName, IpAddress = ip });
            await _db.SaveChangesAsync();
        }
    }
}
