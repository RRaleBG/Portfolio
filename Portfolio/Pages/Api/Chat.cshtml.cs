using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Portfolio.Models;
using Portfolio.Services;
using System.Text.Json;
using Portfolio.Database;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.SignalR;
using Portfolio.Hubs;
using System.IO;

namespace Portfolio.Pages.Api
{
    [AllowAnonymous]
    [IgnoreAntiforgeryToken]
    public class ChatModel : PageModel
    {
        private readonly ILogger<ChatModel> _logger;
        private readonly AIService _ai;
        private readonly ApplicationDbContext _db;
        private readonly IHubContext<NotificationsHub> _hub;
        private const string SessionKey = "chatmem";
        private const string SessionIdKey = "chatsessionid";

        public ChatModel(ILogger<ChatModel> logger, AIService ai, ApplicationDbContext db, IHubContext<NotificationsHub> hub)
        {
            _logger = logger;
            _ai = ai;
            _db = db;
            _hub = hub;
        }

        public class ChatRequest { public string Message { get; set; } = string.Empty; }

        public void OnGet() { }


        public async Task<IActionResult> OnPostAsync([FromBody] ChatRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { error = "Invalid request body." });
            }

            if (string.IsNullOrWhiteSpace(request.Message))
            {
                return BadRequest(new { error = "Message is required." });
            }

            try
            {
                // Ensure we have a session-scoped chat id to persist chat turns
                var sessionId = HttpContext.Session.GetString(SessionIdKey);
                if (string.IsNullOrEmpty(sessionId))
                {
                    sessionId = Guid.NewGuid().ToString();
                    HttpContext.Session.SetString(SessionIdKey, sessionId);
                }

                // Load recent chat history for this session from DB (limit last 20 turns)
                var history = await _db.Chats
                    .Where(c => c.ChatSessionId == sessionId)
                    .OrderBy(c => c.Timestamp)
                    .Take(50)
                    .Select(c => new Chat { Role = c.Role, Content = c.Content, Response = c.Response, ChatSessionId = c.ChatSessionId, Timestamp = c.Timestamp })
                    .ToListAsync();

                // Add user turn and persist
                var userTurn = new Chat { Role = "user", Content = request.Message, ChatSessionId = sessionId, Timestamp = DateTime.UtcNow };
                _db.Chats.Add(userTurn);
                await _db.SaveChangesAsync();

                // Build memory list to pass to AIService (use most recent 20)
                var mem = history.Select(h => new Chat { Role = h.Role, Content = h.Content }).ToList();
                mem.Add(new Chat { Role = "user", Content = request.Message });

                string response;
                try
                {
                    response = await _ai.AskAsync(request.Message, mem);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "AIService failed to produce a response");
                    return StatusCode(502, new { error = "AI service error", detail = ex.Message });
                }

                if (string.IsNullOrWhiteSpace(response))
                {
                    response = "I'm not sure how to answer that.";
                }

                // Persist assistant response
                var assistantTurn = new Chat { Role = "assistant", Content = response, ChatSessionId = sessionId, Timestamp = DateTime.UtcNow };
                _db.Chats.Add(assistantTurn);
                await _db.SaveChangesAsync();

                // Persist QA pair to SavedInteractions for dashboard/analytics
                try
                {
                    var saved = new SavedInteraction { Question = request.Message, Answer = response };
                    _db.SavedInteractions.Add(saved);
                    await _db.SaveChangesAsync();

                    // Broadcast a lightweight notification to connected clients (example)
                    await _hub.Clients.All.SendAsync("ReceiveNotification", new
                    {
                        title = "New chat question",
                        message = request.Message,
                        timestamp = DateTime.UtcNow
                    });
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to save interaction to SavedInteractions");
                }

                var recent = await _db.Chats.Where(c => c.ChatSessionId == sessionId)
                                            .OrderBy(c => c.Timestamp)
                                            .Take(50)
                                            .ToListAsync();

                HttpContext.Session.SetString(SessionKey, JsonSerializer.Serialize(recent));

                return new JsonResult(new { reply = response, response });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing chat request.");

                var devDetail = System.Diagnostics.Debugger.IsAttached ? ex.ToString() : null;

                return StatusCode(500, new { error = "An error occurred while processing your request.", detail = devDetail });
            }
        }
    }
}
