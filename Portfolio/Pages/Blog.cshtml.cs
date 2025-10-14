using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;
using Portfolio.Models;

namespace Portfolio.Pages
{
    public class BlogModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public BlogModel(ApplicationDbContext db) { _db = db; }
        public List<BlogPost> Posts { get; set; } = new();

        [BindProperty]
        public int BlogId { get; set; }
        [BindProperty]
        public int Stars { get; set; }
        [BindProperty]
        public string CommentText { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            await LogVisitAsync("Blog");
            Posts = await _db.BlogPosts.Include(p => p.Comments).OrderByDescending(p => p.PublishedAt).ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (BlogId > 0 && Stars >= 1 && Stars <= 5)
            {
                var blog = await _db.BlogPosts.Include(p => p.Comments).FirstOrDefaultAsync(p => p.Id == BlogId);
                if (blog != null)
                {
                    var comment = new Comment
                    {
                        BlogId = BlogId,
                        Stars = Stars,
                        CommentText = CommentText
                    };
                    _db.Comments.Add(comment);
                    await _db.SaveChangesAsync();
                }
            }
            await OnGetAsync();
            return Page();
        }

        public async Task LogVisitAsync(string pageName)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _db.PageVisits.Add(new PageVisit { Page = pageName, IpAddress = ip });
            await _db.SaveChangesAsync();
        }
    }
}

