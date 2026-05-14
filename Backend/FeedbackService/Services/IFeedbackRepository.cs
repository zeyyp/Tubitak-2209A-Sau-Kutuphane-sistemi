using FeedbackService.Models;

namespace FeedbackService.Services;

public interface IFeedbackRepository
{
    Task<IReadOnlyCollection<Feedback>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<Feedback> AddAsync(Feedback feedback, CancellationToken cancellationToken = default);
}
