namespace FeedbackService.Services;

public interface IAIAnalysisService
{
    Task<FeedbackAnalysisResult> AnalyzeFeedbacksAsync(List<Models.Feedback> feedbacks, CancellationToken cancellationToken = default);
    Task<string> GenerateSummaryAsync(List<Models.Feedback> feedbacks, CancellationToken cancellationToken = default);
}

public class FeedbackAnalysisResult
{
    public int TotalFeedbacks { get; set; }
    public string OverallSummary { get; set; } = string.Empty;
    public string Sentiment { get; set; } = "Neutral"; // Positive, Negative, Neutral
    public List<string> KeyIssues { get; set; } = new();
    public List<string> Suggestions { get; set; } = new();
    public Dictionary<string, int> TopicFrequency { get; set; } = new();
}
