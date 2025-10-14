using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;
using Portfolio.Models;

namespace Portfolio.Pages
{
    public class ProjectsModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public ProjectsModel(ApplicationDbContext db) { _db = db; }

        public List<Project> Projects { get; set; } = new();

        [BindProperty]
        public int ProjectId { get; set; }
        [BindProperty]
        public int Stars { get; set; }
        [BindProperty]
        public string CommentText { get; set; } = string.Empty;

        public async Task OnGetAsync()
        {
            Projects = await _db.Projects.Include(p => p.Comments).OrderByDescending(p => p.Date).ToListAsync();
            await LogVisitAsync("Projects");
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (ProjectId > 0 && Stars >= 1 && Stars <= 5)
            {
                var project = await _db.Projects.Include(p => p.Comments).FirstOrDefaultAsync(p => p.Id == ProjectId);
                if (project != null)
                {
                    var comment = new Comment
                    {
                        ProjectId = ProjectId,
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

        private async Task LogVisitAsync(string pageName)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _db.PageVisits.Add(new PageVisit { Page = pageName, IpAddress = ip });
            await _db.SaveChangesAsync();
        }
    }
}
