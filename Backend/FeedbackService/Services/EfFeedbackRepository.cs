using FeedbackService.Data;
using FeedbackService.Models;
using Microsoft.EntityFrameworkCore;

namespace FeedbackService.Services;

public class EfFeedbackRepository : IFeedbackRepository
{
    private readonly FeedbackDbContext _context;

    public EfFeedbackRepository(FeedbackDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyCollection<Feedback>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        return await _context.Feedbacks
            .OrderByDescending(f => f.Date)
            .ToListAsync(cancellationToken);
    }

    public async Task<Feedback> AddAsync(Feedback feedback, CancellationToken cancellationToken = default)
    {
        var entry = new Feedback
        {
            StudentNumber = feedback.StudentNumber.Trim(),
            Message = feedback.Message.Trim(),
            Date = DateTime.UtcNow
        };

        _context.Feedbacks.Add(entry);
        await _context.SaveChangesAsync(cancellationToken);
        return entry;
    }
}
