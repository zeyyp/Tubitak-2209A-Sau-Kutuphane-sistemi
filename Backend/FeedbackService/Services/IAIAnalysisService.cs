using System.Text.Json.Serialization;

namespace FeedbackService.Services;

public interface IAIAnalysisService
{
    Task<FeedbackAnalysisResult> AnalyzeFeedbacksAsync(List<Models.Feedback> feedbacks, CancellationToken cancellationToken = default);
    Task<string> GenerateSummaryAsync(List<Models.Feedback> feedbacks, CancellationToken cancellationToken = default);
}

public class FeedbackAnalysisResult
{
    [JsonPropertyName("total_feedbacks")]
    public int TotalFeedbacks { get; set; }

    [JsonPropertyName("genel_ozet")]
    public string GenelOzet { get; set; } = string.Empty;

    [JsonPropertyName("sentiment")]
    public SentimentDetail Sentiment { get; set; } = new();

    [JsonPropertyName("kritik_sorunlar")]
    public List<KritikSorun> KritikSorunlar { get; set; } = new();

    [JsonPropertyName("oneriler")]
    public List<OneriDetail> Oneriler { get; set; } = new();

    [JsonPropertyName("aksiyon_plani")]
    public string AksiyonPlani { get; set; } = string.Empty;

    [JsonPropertyName("konu_frekanslari")]
    public List<KonuFrekansi> KonuFrekanslari { get; set; } = new();
}

public class SentimentDetail
{
    [JsonPropertyName("pozitif")]
    public int Pozitif { get; set; }

    [JsonPropertyName("notr")]
    public int Notr { get; set; }

    [JsonPropertyName("negatif")]
    public int Negatif { get; set; }
}

public class KritikSorun
{
    [JsonPropertyName("sorun")]
    public string Sorun { get; set; } = string.Empty;

    [JsonPropertyName("tekrar_sayisi")]
    public int TekrarSayisi { get; set; }

    [JsonPropertyName("oncelik")]
    public string Oncelik { get; set; } = string.Empty; // Yüksek/Orta/Düşük
}

public class OneriDetail
{
    [JsonPropertyName("oneri")]
    public string Oneri { get; set; } = string.Empty;

    [JsonPropertyName("etki")]
    public string Etki { get; set; } = string.Empty; // Yüksek/Orta/Düşük
}

public class KonuFrekansi
{
    [JsonPropertyName("konu")]
    public string Konu { get; set; } = string.Empty;

    [JsonPropertyName("sayi")]
    public int Sayi { get; set; }
}
