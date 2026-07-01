using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PCKManagementSystem.Data;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace PCKManagementSystem.Hubs
{
    public class AuditSettings
    {
        public int RetentionDays { get; set; } = 365;
        public int CleanupIntervalHours { get; set; } = 24;
    }

    public class AuditCleanupService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly ILogger<AuditCleanupService> _logger;
        private readonly IOptions<AuditSettings> _settings;

        public AuditCleanupService(IServiceProvider services, ILogger<AuditCleanupService> logger, IOptions<AuditSettings> settings)
        {
            _services = services;
            _logger = logger;
            _settings = settings;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                await CleanupOldAuditLogs();
                await Task.Delay(TimeSpan.FromHours(_settings.Value.CleanupIntervalHours), stoppingToken);
            }
        }

        private async Task CleanupOldAuditLogs()
        {
            try
            {
                using var scope = _services.CreateScope();
                var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                var cutoffDate = DateTime.UtcNow.AddDays(-_settings.Value.RetentionDays);
                var oldLogs = context.AuditLogs.Where(a => a.ActionDate < cutoffDate);
                var count = await oldLogs.CountAsync();
                if (count > 0)
                {
                    context.AuditLogs.RemoveRange(oldLogs);
                    await context.SaveChangesAsync();
                    _logger.LogInformation($"Очистка аудита: удалено {count} записей старше {_settings.Value.RetentionDays} дней.");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Ошибка при очистке аудита");
            }
        }
    }
}