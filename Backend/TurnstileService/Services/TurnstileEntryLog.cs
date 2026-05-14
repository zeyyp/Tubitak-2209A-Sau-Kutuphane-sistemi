using TurnstileService.Models;

namespace TurnstileService.Services;

public interface ITurnstileEntryLog
{
    void Record(string studentNumber, bool allowed, string message);
    IReadOnlyCollection<TurnstileEntryLogRecord> GetLatest(int take = 20);
}
