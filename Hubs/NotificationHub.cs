using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging; // если используете ILogger

namespace PCKManagementSystem.Hubs
{
    public class NotificationHub : Hub
    {
        private readonly ILogger<NotificationHub> _logger;

        // Внедрение логгера через конструктор (если используете DI)
        public NotificationHub(ILogger<NotificationHub> logger)
        {
            _logger = logger;
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier; // ID пользователя (если аутентифицирован)
            _logger.LogInformation($"Клиент подключился: {Context.ConnectionId}, UserId: {userId}");
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception? exception)
        {
            var userId = Context.UserIdentifier;
            if (exception != null)
            {
                _logger.LogError(exception, $"Клиент отключился с ошибкой: {Context.ConnectionId}, UserId: {userId}");
            }
            else
            {
                _logger.LogInformation($"Клиент отключился: {Context.ConnectionId}, UserId: {userId}");
            }
            await base.OnDisconnectedAsync(exception);
        }
    }
}