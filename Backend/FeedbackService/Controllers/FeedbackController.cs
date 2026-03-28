using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FeedbackService.Models;
using FeedbackService.Services;

namespace FeedbackService.Controllers;

[ApiController]
[Route("api/[controller]")]
public class FeedbackController : ControllerBase
{
    private readonly IFeedbackRepository _repository;
    private readonly IAIAnalysisService _aiService;

    public FeedbackController(IFeedbackRepository repository, IAIAnalysisService aiService)
    {
        _repository = repository;
        _aiService = aiService;
    }

    [HttpPost("Submit")]
    public async Task<IActionResult> SubmitFeedback([FromBody] Feedback feedback, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var saved = await _repository.AddAsync(feedback, cancellationToken);
        return Ok(new { message = "Geri bildiriminiz alındı.", id = saved.Id });
    }

    [HttpGet]
    public async Task<IActionResult> GetFeedbacks([FromQuery] string? studentNumber, CancellationToken cancellationToken)
    {
        var feedbacks = await _repository.GetAllAsync(cancellationToken);
        
        // Eğer studentNumber belirtilmişse, sadece o öğrencinin geri bildirimlerini döndür
        if (!string.IsNullOrEmpty(studentNumber))
        {
            feedbacks = feedbacks.Where(f => f.StudentNumber == studentNumber).ToList();
        }
        
        return Ok(feedbacks);
    }

    [HttpGet("Analysis")]
    public async Task<IActionResult> GetAnalysis(CancellationToken cancellationToken)
    {
        var feedbacks = await _repository.GetAllAsync(cancellationToken);
        var feedbackList = feedbacks.ToList();
        var analysis = await _aiService.AnalyzeFeedbacksAsync(feedbackList, cancellationToken);
        return Ok(analysis);
    }

    [HttpGet("Summary")]
    public async Task<IActionResult> GetSummary(CancellationToken cancellationToken)
    {
        var feedbacks = await _repository.GetAllAsync(cancellationToken);
        var feedbackList = feedbacks.ToList();
        var summary = await _aiService.GenerateSummaryAsync(feedbackList, cancellationToken);
        return Ok(new { summary });
    }
}

