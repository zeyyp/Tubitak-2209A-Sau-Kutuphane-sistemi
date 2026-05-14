namespace TurnstileService.Models;

/// <summary>
/// PostgreSQL kalıcı giriş log kaydı (veritabanı entity'si)
/// </summary>
public class TurnstileEntryLogEntity
{
    public int Id { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public bool Allowed { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
}
