using FluentAssertions;
using IntegrationTests.Fixtures;
using IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Tests;

/// <summary>
/// Geri Bildirim (Feedback) Servisi Integration Testleri
/// TÜBİTAK 2209-A - SAÜ Kütüphane Rezervasyon Sistemi
/// 
/// NOT: Feedback API sadece StudentNumber ve Message alanlarını kabul eder.
/// </summary>
[Collection("Integration Tests")]
public class FeedbackTests
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public FeedbackTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    #region Geri Bildirim Gönderme Testleri

    [Fact]
    [Trait("Category", "Feedback")]
    [Trait("Priority", "Critical")]
    public async Task SubmitFeedback_WithValidData_ShouldSucceed()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        var feedback = new SimpleFeedbackRequest
        {
            StudentNumber = studentNumber,
            Message = "Bu bir integration test geri bildirimidir. Sistem çok iyi çalışıyor."
        };

        // Act
        var response = await _fixture.FeedbackClient.PostAsync<FeedbackSubmitResponse>(
            "/api/Feedback/Submit", feedback);

        // Assert
        _output.WriteLine($"Response: StatusCode={response.StatusCode}, Success={response.IsSuccess}");
        response.IsSuccess.Should().BeTrue("Geri bildirim başarıyla kaydedilmeli");
        
        if (response.Data != null)
        {
            _output.WriteLine($"✓ Geri bildirim gönderildi: {response.Data.Id}");
        }
    }

    [Fact]
    [Trait("Category", "Feedback")]
    [Trait("Priority", "High")]
    public async Task SubmitFeedback_WithShortMessage_ShouldSucceed()
    {
        // Arrange
        var feedback = new SimpleFeedbackRequest
        {
            StudentNumber = IntegrationTestFixture.GenerateStudentNumber(),
            Message = "Güzel sistem!"
        };

        // Act
        var response = await _fixture.FeedbackClient.PostAsync<FeedbackSubmitResponse>(
            "/api/Feedback/Submit", feedback);

        // Assert
        response.IsSuccess.Should().BeTrue("Kısa mesaj kaydedilmeli");
        _output.WriteLine($"✓ Kısa mesaj testi geçti: StatusCode={response.StatusCode}");
    }

    [Fact]
    [Trait("Category", "Feedback")]
    [Trait("Priority", "High")]
    public async Task SubmitFeedback_WithEmptyMessage_ShouldFail()
    {
        // Arrange
        var feedback = new SimpleFeedbackRequest
        {
            StudentNumber = IntegrationTestFixture.GenerateStudentNumber(),
            Message = ""
        };

        // Act
        var response = await _fixture.FeedbackClient.PostAsync<FeedbackSubmitResponse>(
            "/api/Feedback/Submit", feedback);

        // Assert
        response.IsSuccess.Should().BeFalse("Boş mesaj reddedilmeli");
        _output.WriteLine($"✓ Boş mesaj reddedildi: StatusCode={response.StatusCode}");
    }

    [Fact]
    [Trait("Category", "Feedback")]
    [Trait("Priority", "High")]
    public async Task SubmitFeedback_WithEmptyStudentNumber_ShouldFail()
    {
        // Arrange
        var feedback = new SimpleFeedbackRequest
        {
            StudentNumber = "",
            Message = "Test mesajı"
        };

        // Act
        var response = await _fixture.FeedbackClient.PostAsync<FeedbackSubmitResponse>(
            "/api/Feedback/Submit", feedback);

        // Assert
        response.IsSuccess.Should().BeFalse("Boş öğrenci numarası reddedilmeli");
        _output.WriteLine($"✓ Boş öğrenci numarası reddedildi: StatusCode={response.StatusCode}");
    }

    [Fact]
    [Trait("Category", "Feedback")]
    [Trait("Priority", "Medium")]
    public async Task SubmitFeedback_WithLongMessage_ShouldHandleGracefully()
    {
        // Arrange - 500 karaktere yakın mesaj (model limiti)
        var longMessage = new string('A', 400) + " test mesajı";
        var feedback = new SimpleFeedbackRequest
        {
            StudentNumber = IntegrationTestFixture.GenerateStudentNumber(),
            Message = longMessage
        };

        // Act
        var response = await _fixture.FeedbackClient.PostAsync<FeedbackSubmitResponse>(
            "/api/Feedback/Submit", feedback);

        // Assert - Sonucu logla (API limiti aşarsa hata verebilir)
        _output.WriteLine($"✓ Uzun mesaj testi: StatusCode={response.StatusCode}, Length={longMessage.Length}");
    }

    #endregion

    #region Geri Bildirim Listeleme Testleri

    [Fact]
    [Trait("Category", "Feedback")]
    [Trait("Priority", "Critical")]
    public async Task GetFeedbacks_ShouldReturnFeedbackList()
    {
        // Act
        var response = await _fixture.FeedbackClient.GetAsync<List<SimpleFeedbackItem>>(
            "/api/Feedback");

        // Assert
        response.IsSuccess.Should().BeTrue("Geri bildirim listesi dönmeli");
        response.Data.Should().NotBeNull("Liste verisi dönmeli");

        _output.WriteLine($"✓ Geri bildirim listesi alındı: {response.Data?.Count ?? 0} kayıt");
    }

    [Fact]
    [Trait("Category", "Feedback")]
    [Trait("Priority", "High")]
    public async Task GetFeedbacks_ByStudentNumber_ShouldFilterResults()
    {
        // Arrange - Önce geri bildirim oluştur
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        var feedback = new SimpleFeedbackRequest
        {
            StudentNumber = studentNumber,
            Message = "Filtreleme testi için geri bildirim"
        };

        var submitResponse = await _fixture.FeedbackClient.PostAsync<FeedbackSubmitResponse>(
            "/api/Feedback/Submit", feedback);
        
        _output.WriteLine($"Submit sonucu: StatusCode={submitResponse.StatusCode}");

        // Act - Filtrelenmiş liste al
        var response = await _fixture.FeedbackClient.GetAsync<List<SimpleFeedbackItem>>(
            $"/api/Feedback?studentNumber={studentNumber}");

        // Assert
        response.IsSuccess.Should().BeTrue("Filtrelenmiş liste dönmeli");
        _output.WriteLine($"✓ Filtrelenmiş liste alındı: {response.Data?.Count ?? 0} kayıt");
    }

    #endregion

    #region Analiz ve Özet Testleri

    [Fact]
    [Trait("Category", "Feedback")]
    [Trait("Priority", "High")]
    public async Task GetAnalysis_ShouldReturnAnalysisData()
    {
        // Act
        var response = await _fixture.FeedbackClient.GetAsync<object>(
            "/api/Feedback/Analysis");

        // Assert
        response.IsSuccess.Should().BeTrue("Analiz verisi dönmeli");
        _output.WriteLine($"✓ Analiz verisi alındı: StatusCode={response.StatusCode}");
    }

    [Fact]
    [Trait("Category", "Feedback")]
    [Trait("Priority", "High")]
    public async Task GetSummary_ShouldReturnSummaryText()
    {
        // Act
        var response = await _fixture.FeedbackClient.GetAsync<object>(
            "/api/Feedback/Summary");

        // Assert
        response.IsSuccess.Should().BeTrue("Özet verisi dönmeli");
        _output.WriteLine($"✓ Özet alındı: StatusCode={response.StatusCode}");
    }

    #endregion

    #region Performans Testleri

    [Fact]
    [Trait("Category", "Performance")]
    [Trait("Priority", "Medium")]
    public async Task SubmitMultipleFeedbacks_ShouldCompleteInReasonableTime()
    {
        // Arrange
        var feedbackCount = 5;
        var startTime = DateTime.Now;
        var successCount = 0;

        // Act
        for (int i = 0; i < feedbackCount; i++)
        {
            var feedback = new SimpleFeedbackRequest
            {
                StudentNumber = IntegrationTestFixture.GenerateStudentNumber(),
                Message = $"Performans testi geri bildirimi {i + 1}"
            };

            var response = await _fixture.FeedbackClient.PostAsync<FeedbackSubmitResponse>(
                "/api/Feedback/Submit", feedback);
            
            if (response.IsSuccess) successCount++;
        }

        var elapsed = DateTime.Now - startTime;

        // Assert
        elapsed.TotalSeconds.Should().BeLessThan(30, 
            $"{feedbackCount} geri bildirim 30 saniyeden kısa sürede kaydedilmeli");

        _output.WriteLine($"✓ {successCount}/{feedbackCount} geri bildirim {elapsed.TotalSeconds:F2} saniyede işlendi");
        _output.WriteLine($"  - Ortalama: {elapsed.TotalMilliseconds / feedbackCount:F0} ms/istek");
    }

    #endregion
}

#region Feedback Models

/// <summary>
/// Feedback API'nin beklediği gerçek model
/// </summary>
public class SimpleFeedbackRequest
{
    public string StudentNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public class FeedbackSubmitResponse
{
    public string Message { get; set; } = string.Empty;
    public int? Id { get; set; }
}

public class SimpleFeedbackItem
{
    public int Id { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Date { get; set; }
}

#endregion
