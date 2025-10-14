using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;

namespace Portfolio.Pages.Admin.Users
{
    public class IndexModel(ILogger<IndexModel> logger, ApplicationDbContext context) : PageModel
    {
        private readonly ILogger<IndexModel> _logger = logger;
        private readonly ApplicationDbContext _context = context;
        public List<IdentityUser> Users { get; set; } = new();


        public async Task<IActionResult> OnGetAsync()
        {
            _logger.LogInformation("Accessed Admin Users Index page.");

            Users = await _context.Users.ToListAsync();

            return Page();
        }

        // AJAX delete handler
        public async Task<IActionResult> OnPostDeleteAsync([FromBody] DeleteUserRequest request)
        {
            var user = await _context.Users.FindAsync(request.id);
            if (user != null)
            {
                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                return new JsonResult(new { success = true });
            }
            return new JsonResult(new { success = false });
        }

        public class DeleteUserRequest
        {
            public string id { get; set; }
        }
    }
}
