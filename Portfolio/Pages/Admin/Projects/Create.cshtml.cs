using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Database;
using Portfolio.Models;

namespace Portfolio.Pages.Admin.Projects
{
    [Authorize]
    public class CreateModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public CreateModel(ApplicationDbContext db) { _db = db; }
        [BindProperty]
        public Project Item { get; set; } = new();
        public void OnGet() { }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();
            _db.Projects.Add(Item);
            await _db.SaveChangesAsync();
            return RedirectToPage("Index");
        }
    }
}
