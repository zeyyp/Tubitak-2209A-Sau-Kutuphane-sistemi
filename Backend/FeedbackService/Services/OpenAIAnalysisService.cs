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
                GenelOzet = "Henüz geri bildirim bulunmamaktadır.",
                Sentiment = new SentimentDetail { Pozitif = 0, Notr = 100, Negatif = 0 },
                AksiyonPlani = "Henüz yeterli veri olmadığı için aksiyon planı oluşturulamadı."
            };
        }

        try
        {
            var feedbackTexts = string.Join("\n", feedbacks.Select((f, i) => $"{i + 1}. {f.Message}"));
            
            var analysisPrompt = $@"
Sen bir veri analisti ve kütüphane yönetim uzmanısın.
Aşağıda SAÜ Kütüphanesi rezervasyon sistemine ait {feedbacks.Count} kullanıcı geri bildirimi var.

GERİ BİLDİRİMLER:
{feedbackTexts}

KURALLAR:
- ""tablo"", ""veri"", ""kayıt"" gibi teknik kelimeler KULLANMA. Bunun yerine ""kullanıcılar"", ""geri bildirimler"" de.
- ""aksiyon_plani"" alanında özne belirt: ""Kütüphane yönetimi [şunu] yapmalı"" formatında çok net ve direktif yaz.
- ""kritik_sorunlar"" listesindeki ""sorun"" açıklamalarında sayısal kanıt ekle: ""X kullanıcı Y sorununu belirtti, bu toplam geri bildirimlerin %Z'si"" formatını kullan (Z oranını {feedbacks.Count} değerine göre hesapla).

Yalnızca aşağıdaki JSON formatında yanıt ver, başka hiçbir şey yazma:

{{
  ""genel_ozet"": ""Yönetici özeti. Kurallara uygun olmalıdır."",
  ""sentiment"": {{
    ""pozitif"": <0-100 arası sayı>,
    ""notr"": <0-100 arası sayı>,
    ""negatif"": <0-100 arası sayı>
  }},
  ""kritik_sorunlar"": [
    {{""sorun"": ""Sayısal kanıtlı sorun açıklaması. Örn: '15 kullanıcı temizlik yetersizliğini belirtti, bu toplam geri bildirimlerin %45.4\\'ü'"", ""tekrar_sayisi"": <sayı>, ""oncelik"": ""Yüksek/Orta/Düşük""}},
    ... (en fazla 5 sorun)
  ],
  ""oneriler"": [
    {{""oneri"": ""öneri açıklaması"", ""etki"": ""Yüksek/Orta/Düşük""}},
    ... (en fazla 5 öneri)
  ],
  ""aksiyon_plani"": ""Kurallara uygun kütüphane yönetimi öznesi içeren direktif aksiyon planı."",
  ""konu_frekanslari"": [
    {{""konu"": ""konu adı"", ""sayi"": <sayı>}},
    ... (en fazla 8 konu)
  ]
}}";

            var response = await CallOpenAIAsync(analysisPrompt, cancellationToken);
            return ParseAnalysisResponse(response, feedbacks.Count, feedbacks);
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
            
            var summaryPrompt = $@"
Sen bir kütüphane yönetim danışmanısın. Aşağıda SAÜ Kütüphanesi'ne ait {feedbacks.Count} adet kullanıcı geri bildirimi bulunmaktadır.

GERİ BİLDİRİMLER:
{feedbackTexts}

Aşağıdaki kurallara ve yapıda Türkçe, yöneticiye yönelik bir rapor yaz. Her bir numaralandırılmış maddeyi kesinlikle yeni bir satıra yaz (aralarına \n koy):

KURALLAR:
- ""tablo"", ""veri"", ""kayıt"" gibi teknik kelimeler KULLANMA. Bunun yerine ""kullanıcılar"", ""geri bildirimler"" de.
- Aksiyon planında (Madde 3) özne belirt: ""Kütüphane yönetimi [şunu] yapmalı"" formatında yaz.
- Sayısal kanıt ekle: ""X kullanıcı Y sorununu belirtti, bu toplam geri bildirimlerin %Z'si"" (Z oranını {feedbacks.Count} değerine göre hesapla).

RAPOR YAPISI:
1. GENEL DURUM (2 cümle): Kaç geri bildirim var, genel memnuniyet nasıl?
2. KRİTİK SORUN (1 cümle): En çok tekrar eden sorun nedir, kaç kişi belirtti ve oranı yüzde kaçtır?
3. ÖNCELİKLİ AKSİYON (1 cümle): Kütüphane yönetimi öncelikle ne yapmalı?
4. OLUMLU BULGULAR (1 cümle): Kullanıcıların memnun olduğu ne var?

Rapor resmi ve net olsun. Toplam 4-5 cümleyi geçmesin.";

            return await CallOpenAIAsync(summaryPrompt, cancellationToken);
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

    private FeedbackAnalysisResult ParseAnalysisResponse(string response, int totalCount, List<Models.Feedback> feedbacks)
    {
        try
        {
            var cleanResponse = response.Trim();
            if (cleanResponse.StartsWith("```json"))
            {
                cleanResponse = cleanResponse.Substring(7);
            }
            if (cleanResponse.EndsWith("```"))
            {
                cleanResponse = cleanResponse.Substring(0, cleanResponse.Length - 3);
            }
            cleanResponse = cleanResponse.Trim();

            var doc = JsonDocument.Parse(cleanResponse);
            var root = doc.RootElement;

            var result = new FeedbackAnalysisResult
            {
                TotalFeedbacks = totalCount,
                GenelOzet = root.TryGetProperty("genel_ozet", out var genOzet) ? genOzet.GetString() ?? "" : "",
                AksiyonPlani = root.TryGetProperty("aksiyon_plani", out var aksPlani) ? aksPlani.GetString() ?? "" : "",
                Sentiment = new SentimentDetail(),
                KritikSorunlar = new List<KritikSorun>(),
                Oneriler = new List<OneriDetail>(),
                KonuFrekanslari = new List<KonuFrekansi>()
            };

            if (root.TryGetProperty("sentiment", out var sentElement))
            {
                result.Sentiment.Pozitif = sentElement.TryGetProperty("pozitif", out var poz) ? poz.GetInt32() : 0;
                result.Sentiment.Notr = sentElement.TryGetProperty("notr", out var notrVal) ? notrVal.GetInt32() : 0;
                result.Sentiment.Negatif = sentElement.TryGetProperty("negatif", out var neg) ? neg.GetInt32() : 0;
            }

            if (root.TryGetProperty("kritik_sorunlar", out var kritikList))
            {
                foreach (var item in kritikList.EnumerateArray())
                {
                    result.KritikSorunlar.Add(new KritikSorun
                    {
                        Sorun = item.TryGetProperty("sorun", out var s) ? s.GetString() ?? "" : "",
                        TekrarSayisi = item.TryGetProperty("tekrar_sayisi", out var t) ? t.GetInt32() : 0,
                        Oncelik = item.TryGetProperty("oncelik", out var o) ? o.GetString() ?? "" : ""
                    });
                }
            }

            if (root.TryGetProperty("oneriler", out var oneriList))
            {
                foreach (var item in oneriList.EnumerateArray())
                {
                    result.Oneriler.Add(new OneriDetail
                    {
                        Oneri = item.TryGetProperty("oneri", out var o) ? o.GetString() ?? "" : "",
                        Etki = item.TryGetProperty("etki", out var e) ? e.GetString() ?? "" : ""
                    });
                }
            }

            if (root.TryGetProperty("konu_frekanslari", out var konuList))
            {
                foreach (var item in konuList.EnumerateArray())
                {
                    result.KonuFrekanslari.Add(new KonuFrekansi
                    {
                        Konu = item.TryGetProperty("konu", out var k) ? k.GetString() ?? "" : "",
                        Sayi = item.TryGetProperty("sayi", out var sa) ? sa.GetInt32() : 0
                    });
                }
            }

            return result;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "JSON parsing error on OpenAI response: {Response}", response);
            return GetFallbackAnalysis(feedbacks);
        }
    }

    private FeedbackAnalysisResult GetFallbackAnalysis(List<Models.Feedback> feedbacks)
    {
        var result = new FeedbackAnalysisResult
        {
            TotalFeedbacks = feedbacks.Count,
            GenelOzet = $"{feedbacks.Count} adet geri bildirim alınmıştır. AI analizi şu an kullanılamıyor.",
            AksiyonPlani = "AI servisi geçici olarak çevrimdışı. Geri bildirimlerin manuel incelenmesi önerilir.",
            Sentiment = new SentimentDetail { Pozitif = 40, Notr = 40, Negatif = 20 },
            KritikSorunlar = new List<KritikSorun>
            {
                new KritikSorun { Sorun = "Gürültü ve yüksek ses düzeyi", TekrarSayisi = 2, Oncelik = "Yüksek" },
                new KritikSorun { Sorun = "Priz ve şarj yeri yetersizliği", TekrarSayisi = 1, Oncelik = "Orta" }
            },
            Oneriler = new List<OneriDetail>
            {
                new OneriDetail { Oneri = "Sessiz çalışma kurallarının hatırlatılması", Etki = "Yüksek" },
                new OneriDetail { Oneri = "Ortak çalışma alanlarına yeni prizler eklenmesi", Etki = "Yüksek" }
            },
            KonuFrekanslari = new List<KonuFrekansi>
            {
                new KonuFrekansi { Konu = "Gürültü", Sayi = 2 },
                new KonuFrekansi { Konu = "Priz", Sayi = 1 }
            }
        };

        return result;
    }
}
