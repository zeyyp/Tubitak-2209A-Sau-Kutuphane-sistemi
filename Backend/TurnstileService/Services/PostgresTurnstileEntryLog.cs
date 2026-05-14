using Microsoft.EntityFrameworkCore;
using TurnstileService.Data;
using TurnstileService.Models;

namespace TurnstileService.Services;

/// <summary>
/// PostgreSQL tabanlı kalıcı turnike giriş log deposu.
/// Tüm girişler veritabanında saklanır; servis yeniden başlatılsa dahi veriler korunur.
/// </summary>
public class PostgresTurnstileEntryLog : ITurnstileEntryLog
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<PostgresTurnstileEntryLog> _logger;

    public PostgresTurnstileEntryLog(IServiceProvider serviceProvider, ILogger<PostgresTurnstileEntryLog> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    public void Record(string studentNumber, bool allowed, string message)
    {
        // Fire-and-forget: arka planda veritabanına yaz; request'i bloklamaz
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _serviceProvider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<TurnstileDbContext>();
                db.EntryLogs.Add(new TurnstileEntryLogEntity
                {
                    StudentNumber = studentNumber,
                    Allowed = allowed,
                    Message = message,
                    TimestampUtc = DateTime.UtcNow
                });
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Turnike log kaydedilemedi. StudentNumber: {StudentNumber}", studentNumber);
            }
        });
    }

    public IReadOnlyCollection<TurnstileEntryLogRecord> GetLatest(int take = 20)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<TurnstileDbContext>();
            return db.EntryLogs
                .OrderByDescending(e => e.TimestampUtc)
                .Take(Math.Max(1, take))
                .Select(e => new TurnstileEntryLogRecord
                {
                    StudentNumber = e.StudentNumber,
                    Allowed = e.Allowed,
                    Message = e.Message,
                    TimestampUtc = e.TimestampUtc
                })
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Turnike logları alınırken hata oluştu.");
            return Array.Empty<TurnstileEntryLogRecord>();
        }
    }
}
