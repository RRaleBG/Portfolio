using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Data;

namespace Portfolio.Pages.Dashboard
{
    [Authorize]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public IndexModel(ApplicationDbContext db) { _db = db; }

        public int CountProjects { get; set; }
        public int CountPosts { get; set; }
        public int CountInteractions { get; set; }

        public async Task OnGet()
        {
            CountProjects = await _db.Projects.CountAsync();
            CountPosts = await _db.BlogPosts.CountAsync();
            CountInteractions = await _db.SavedInteractions.CountAsync();
        }
    }
}
