using Microsoft.AspNetCore.SignalR;

namespace Portfolio.Hubs
{
    public class NotificationsHub(ILogger<NotificationsHub> logger) : Hub
    {
        private readonly ILogger<NotificationsHub> _logger = logger;

        public override async Task OnConnectedAsync()
        {
            _logger.LogInformation("SignalR connected: {ConnectionId} User: {User}", Context.ConnectionId, Context.UserIdentifier ?? "<anon>");

            // If the user is authenticated, add to a user-specific group for targeted messages
            if (!string.IsNullOrEmpty(Context.UserIdentifier))
            {
                await Groups.AddToGroupAsync(Context.ConnectionId, $"user:{Context.UserIdentifier}");
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            _logger.LogInformation("SignalR disconnected: {ConnectionId} User: {User}", Context.ConnectionId, Context.UserIdentifier ?? "<anon>");
            if (!string.IsNullOrEmpty(Context.UserIdentifier))
            {
                await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"user:{Context.UserIdentifier}");
            }
            await base.OnDisconnectedAsync(exception);
        }

        // Simple broadcast API callable from server-side via IHubContext<NotificationsHub>
        public Task SendToAll(string title, string message)
            => Clients.All.SendAsync("ReceiveNotification", new { title, message, timestamp = DateTime.UtcNow });

        // Send to a specific authenticated user (server should use the user's Id)
        public Task SendToUser(string userId, string title, string message)
            => Clients.Group($"user:{userId}").SendAsync("ReceiveNotification", new { title, message, timestamp = DateTime.UtcNow });

        // Send to an arbitrary group
        public Task SendToGroup(string groupName, string title, string message)
            => Clients.Group(groupName).SendAsync("ReceiveNotification", new { title, message, timestamp = DateTime.UtcNow });

        public async Task NotifyUser(string userId, string message)
        {
            await Clients.User(userId).SendAsync("ReceiveNotification", message);
        }
    }
}
