using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;
using Portfolio.Models;

namespace Portfolio.Pages.Admin.Projects
{
    [Authorize]
    public class EditModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        public EditModel(ApplicationDbContext db) { _db = db; }
        [BindProperty]
        public Project Item { get; set; } = new();
        public async Task<IActionResult> OnGetAsync(int id)
        {
            var item = await _db.Projects.FindAsync(id);
            if (item == null) 
                return NotFound();

            Item = item;

            return Page();
        }
        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) 
                return Page();
            
            _db.Attach(Item).State = EntityState.Modified;
            await _db.SaveChangesAsync();
            TempData["ToastMessage"] = "Succesfully changed. Thank you!";
            TempData["ToastType"] = "success";
            return RedirectToPage("Index");
        }
    }
}
