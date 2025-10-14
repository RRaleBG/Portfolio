using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging;
using Portfolio.Database;
using Portfolio.Models;
using Portfolio.Helpers;
using System.Threading.Tasks;

namespace Portfolio.Pages.Admin
{
    [Authorize(Roles = "Admin")]
    public class DbStatusModel : PageModel
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<IdentityUser> _userMgr;
        private readonly RoleManager<IdentityRole> _roleMgr;
        private readonly ILogger<DbStatusModel> _logger;
        private readonly IServiceProvider _sp;
        public int UserCount { get; set; }
        public int RoleCount { get; set; }
        public int ProjectCount { get; set; }
        public int BlogPostCount { get; set; }
        public int ContactCount { get; set; }
        public string AdminEmailStatus { get; set; }
        public string StatusMessage { get; set; }

        public DbStatusModel(ApplicationDbContext db, UserManager<IdentityUser> userMgr, RoleManager<IdentityRole> roleMgr, ILogger<DbStatusModel> logger, IServiceProvider sp)
        {
            _db = db;
            _userMgr = userMgr;
            _roleMgr = roleMgr;
            _logger = logger;
            _sp = sp;
        }

        public async Task OnGetAsync()
        {
            await LoadCountsAsync();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var dbPath = DatabaseInitializer.GetDatabasePath(HttpContext.RequestServices.GetService<IWebHostEnvironment>().ContentRootPath, _logger);
            await DatabaseInitializer.ForceRecreateAndSeedDatabaseAsync(_sp, _logger, dbPath);
            StatusMessage = "Database reseeded successfully.";
            await LoadCountsAsync();
            return Page();
        }

        private async Task LoadCountsAsync()
        {
            UserCount = _db.Users.Count();
            RoleCount = _db.Roles.Count();
            ProjectCount = _db.Projects.Count();
            BlogPostCount = _db.BlogPosts.Count();
            ContactCount = _db.Contacts.Count();

            var adminUser = await _userMgr.FindByEmailAsync("rajcicrados@hotmail.com");
            if (adminUser != null)
            {
                var isAdmin = await _userMgr.IsInRoleAsync(adminUser, "Admin");
                AdminEmailStatus = isAdmin ? "Admin user exists and is assigned to Admin role." : "Admin user exists but is NOT assigned to Admin role.";
            }
            else
            {
                AdminEmailStatus = "Admin user (rajcicrados@hotmail.com) does NOT exist.";
            }
        }
    }
}
