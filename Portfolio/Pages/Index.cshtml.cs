using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;
using Portfolio.Models;

namespace Portfolio.Pages;

public class IndexModel : PageModel
{
    private readonly ApplicationDbContext _db;
    public IndexModel(ApplicationDbContext db) { _db = db; }

    public List<Project> Projects { get; private set; } = new();

    public async Task OnGet()
    {
        Projects = await _db.Projects
            .OrderByDescending(p => p.Date)
            .ToListAsync();
    }
}
