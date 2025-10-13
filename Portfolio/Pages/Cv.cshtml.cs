using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Services;
using Portfolio.Models;
using Portfolio.Data;

namespace Portfolio.Pages
{
    public class CvModel : PageModel
    {
        private readonly ILocalModelClient _localModel;
        private readonly ApplicationDbContext _db;

        public CvModel(ILocalModelClient localModel, ApplicationDbContext db)
        {
            _localModel = localModel;
            _db = db;
        }

        [BindProperty]
        public string UserMessage { get; set; } = string.Empty;

        public List<Chat> Messages { get; set; } = new();
        public string? ErrorMessage { get; set; }

        public void OnGet()
        {
            Messages = _db.Chats
                .Where(c => c.Role == "cv_user" || c.Role == "cv_assistant")
                .OrderByDescending(c => c.Timestamp)
                .Take(20)
                .ToList();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(UserMessage))
            {
                ErrorMessage = "Message cannot be empty.";
                Messages = _db.Chats
                    .Where(c => c.Role == "cv_user" || c.Role == "cv_assistant")
                    .OrderByDescending(c => c.Timestamp)
                    .Take(20)
                    .ToList();
                return Page();
            }

            var response = await _localModel.GenerateAsync(UserMessage);

            var userChat = new Chat { Role = "cv_user", Content = UserMessage, Timestamp = DateTime.UtcNow };
            var botChat = new Chat { Role = "cv_assistant", Content = response, Timestamp = DateTime.UtcNow };
            _db.Chats.Add(userChat);
            _db.Chats.Add(botChat);
            await _db.SaveChangesAsync();

            Messages = _db.Chats
                .Where(c => c.Role == "cv_user" || c.Role == "cv_assistant")
                .OrderByDescending(c => c.Timestamp)
                .Take(20)
                .ToList();
            return Page();
        }
    }
}
