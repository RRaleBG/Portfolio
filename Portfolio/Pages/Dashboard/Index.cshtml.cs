using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;
using System.Linq;
using System;
using Microsoft.AspNetCore.Http;
using Portfolio.Models;

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
        public int TotalVisits { get; set; }
        public int CurrentVisitors { get; set; }
        public int TotalMailSent { get; set; }
        public Dictionary<string, int> PageVisitCounts { get; set; } = new();
        public List<PageVisit> RecentVisits { get; set; } = new();

        public async Task OnGetAsync()
        {
            await LogVisitAsync("Dashboard");
            CountProjects = await _db.Projects.CountAsync();
            CountPosts = await _db.BlogPosts.CountAsync();
            CountInteractions = await _db.SavedInteractions.CountAsync();

            TotalVisits = await _db.PageVisits.CountAsync();
            TotalMailSent = await _db.MailSendLogs.CountAsync();

            // Current visitors: last 10 minutes
            var since = DateTime.UtcNow.AddMinutes(-10);
            CurrentVisitors = await _db.PageVisits.Where(v => v.Timestamp > since).Select(v => v.IpAddress).Distinct().CountAsync();

            // Visits per page
            PageVisitCounts = await _db.PageVisits
                .GroupBy(v => v.Page)
                .Select(g => new { Page = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.Page, x => x.Count);

            RecentVisits = await _db.PageVisits
                .OrderByDescending(v => v.Timestamp)
                .Take(20)
                .ToListAsync();
        }

        public async Task LogVisitAsync(string pageName)
        {
            var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
            _db.PageVisits.Add(new PageVisit { Page = pageName, IpAddress = ip });
            await _db.SaveChangesAsync();
        }
    }
}
