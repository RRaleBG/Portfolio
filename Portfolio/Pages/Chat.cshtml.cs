using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Database;
using Portfolio.Models;
using Portfolio.Services;

namespace Portfolio.Pages
{
    public class ChatModel : PageModel
    {
        private readonly ILocalModelClient _localModel;
        private readonly ApplicationDbContext _db;

        public ChatModel(ILocalModelClient localModel, ApplicationDbContext db)
        {
            _localModel = localModel;
            _db = db;
        }

        [BindProperty]
        public string UserMessage { get; set; } = string.Empty;

        public List<Chat> Messages { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public async Task OnGetAsync()
        {
            await LogVisitAsync("Chat");
            Messages = _db.Chats.OrderByDescending(c => c.Timestamp).Take(20).ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(UserMessage))
            {
                ErrorMessage = "Message cannot be empty.";
                Messages = _db.Chats.OrderByDescending(c => c.Timestamp).Take(20).ToList();
                return Page();
            }

            // Call local model
            var response = await _localModel.GenerateAsync(UserMessage);

            // Save user message and response to DB
            var userChat = new Chat { Role = "user", Content = UserMessage, Timestamp = DateTime.UtcNow };
            var botChat = new Chat { Role = "assistant", Content = response, Timestamp = DateTime.UtcNow };
            _db.Chats.Add(userChat);
            _db.Chats.Add(botChat);
            await _db.SaveChangesAsync();

            Messages = _db.Chats.OrderByDescending(c => c.Timestamp).Take(20).ToList();
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
