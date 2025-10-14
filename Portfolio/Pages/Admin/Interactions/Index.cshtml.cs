using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;
using Portfolio.Models;

namespace Portfolio.Pages.Admin.Interactions
{
    [Authorize(Roles = "Admin")]
    public class IndexModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public IndexModel(ApplicationDbContext db) => _db = db;

        public IList<SavedInteraction> Interactions { get; set; } = new List<SavedInteraction>();

        public async Task OnGetAsync()
        {
            Interactions = await _db.SavedInteractions
                .AsNoTracking()
                .OrderByDescending(i => i.CreatedAt)
                .Take(200)
                .ToListAsync();
        }
    }
}
