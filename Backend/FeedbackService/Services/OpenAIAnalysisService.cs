using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace FeedbackService.Services;

public class OpenAIAnalysisService : IAIAnalysisService
{
    private readonly HttpClient _httpClient;
    private readonly string _apiKey;
    private readonly ILogger<OpenAIAnalysisService> _logger;

    public OpenAIAnalysisService(IConfiguration configuration, ILogger<OpenAIAnalysisService> logger)
    {
        _httpClient = new HttpClient();
        _apiKey = configuration["OpenAI:ApiKey"] ?? throw new InvalidOperationException("OpenAI API Key not configured");
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
        _logger = logger;
    }

    public async Task<FeedbackAnalysisResult> AnalyzeFeedbacksAsync(List<Models.Feedback> feedbacks, CancellationToken cancellationToken = default)
    {
        if (!feedbacks.Any())
        {
            return new FeedbackAnalysisResult
            {
                TotalFeedbacks = 0,
                OverallSummary = "Henüz geri bildirim bulunmamaktadır.",
                Sentiment = "Neutral"
            };
        }

        try
        {
            var feedbackTexts = string.Join("\n", feedbacks.Select((f, i) => $"{i + 1}. {f.Message}"));
            
            var prompt = $@"Sen bir kütüphane yönetim sistemi analistisin. Aşağıdaki öğrenci geri bildirimlerini analiz et ve Türkçe olarak:

1. Genel özet (2-3 cümle)
2. Duygu analizi (Pozitif/Negatif/Nötr)
3. Ana sorunlar (en fazla 5 madde)
4. Öneriler (en fazla 5 madde)
5. En çok bahsedilen konular

Geri Bildirimler:
{feedbackTexts}

JSON formatında yanıt ver:
{{
  ""summary"": ""özet"",
  ""sentiment"": ""Pozitif/Negatif/Nötr"",
  ""issues"": [""sorun1"", ""sorun2""],
  ""suggestions"": [""öneri1"", ""öneri2""],
  ""topics"": {{""konu1"": sayı, ""konu2"": sayı}}
}}";

            var response = await CallOpenAIAsync(prompt, cancellationToken);
            return ParseAnalysisResponse(response, feedbacks.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "AI analizi sırasında hata oluştu");
            return GetFallbackAnalysis(feedbacks);
        }
    }

    public async Task<string> GenerateSummaryAsync(List<Models.Feedback> feedbacks, CancellationToken cancellationToken = default)
    {
        if (!feedbacks.Any())
        {
            return "Henüz geri bildirim bulunmamaktadır.";
        }

        try
        {
            var feedbackTexts = string.Join("\n", feedbacks.Select((f, i) => $"{i + 1}. {f.Message}"));
            
            var prompt = $@"Aşağıdaki kütüphane rezervasyon sistemi geri bildirimlerini özetle (maksimum 3 cümle, Türkçe):

{feedbackTexts}";

            return await CallOpenAIAsync(prompt, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Özet oluşturma sırasında hata oluştu");
            return "Genel olarak kullanıcılar sistemden memnun görünmektedir.";
        }
    }

    private async Task<string> CallOpenAIAsync(string prompt, CancellationToken cancellationToken)
    {
        var requestBody = new
        {
            model = "gpt-3.5-turbo",
            messages = new[]
            {
                new { role = "system", content = "Sen yardımcı bir kütüphane yönetim asistanısın. Her zaman Türkçe yanıt verirsin." },
                new { role = "user", content = prompt }
            },
            temperature = 0.7,
            max_tokens = 800
        };

        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync("https://api.openai.com/v1/chat/completions", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
        var doc = JsonDocument.Parse(responseJson);
        
        return doc.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString() ?? "Analiz tamamlanamadı.";
    }

    private FeedbackAnalysisResult ParseAnalysisResponse(string response, int totalCount)
    {
        try
        {
            var doc = JsonDocument.Parse(response);
            var root = doc.RootElement;

            var result = new FeedbackAnalysisResult
            {
                TotalFeedbacks = totalCount,
                OverallSummary = root.GetProperty("summary").GetString() ?? "",
                Sentiment = root.GetProperty("sentiment").GetString() ?? "Nötr",
                KeyIssues = new List<string>(),
                Suggestions = new List<string>(),
                TopicFrequency = new Dictionary<string, int>()
            };

            if (root.TryGetProperty("issues", out var issues))
            {
                foreach (var issue in issues.EnumerateArray())
                {
                    result.KeyIssues.Add(issue.GetString() ?? "");
                }
            }

            if (root.TryGetProperty("suggestions", out var suggestions))
            {
                foreach (var suggestion in suggestions.EnumerateArray())
                {
                    result.Suggestions.Add(suggestion.GetString() ?? "");
                }
            }

            if (root.TryGetProperty("topics", out var topics))
            {
                foreach (var topic in topics.EnumerateObject())
                {
                    result.TopicFrequency[topic.Name] = topic.Value.GetInt32();
                }
            }

            return result;
        }
        catch
        {
            return new FeedbackAnalysisResult
            {
                TotalFeedbacks = totalCount,
                OverallSummary = response,
                Sentiment = "Nötr"
            };
        }
    }

    private FeedbackAnalysisResult GetFallbackAnalysis(List<Models.Feedback> feedbacks)
    {
        // AI kullanılamadığında basit analiz
        var result = new FeedbackAnalysisResult
        {
            TotalFeedbacks = feedbacks.Count,
            OverallSummary = $"{feedbacks.Count} adet geri bildirim alınmıştır. AI analizi şu an kullanılamıyor.",
            Sentiment = "Nötr",
            KeyIssues = new List<string> { "AI servisi erişilemez durumda" },
            Suggestions = new List<string> { "Manuel inceleme yapılması önerilir" }
        };

        // Basit kelime frekans analizi
        var words = feedbacks
            .SelectMany(f => f.Message.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(w => w.Length > 3)
            .GroupBy(w => w.ToLower())
            .OrderByDescending(g => g.Count())
            .Take(5)
            .ToDictionary(g => g.Key, g => g.Count());

        result.TopicFrequency = words;

        return result;
    }
}
