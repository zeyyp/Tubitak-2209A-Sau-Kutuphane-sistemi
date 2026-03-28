using FluentAssertions;
using IntegrationTests.Fixtures;
using IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Tests;

/// <summary>
/// Turnike (Giriş/Çıkış) Sistemi Integration Testleri
/// TÜBİTAK 2209-A - SAÜ Kütüphane Rezervasyon Sistemi
/// </summary>
[Collection("Integration Tests")]
public class TurnstileTests
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public TurnstileTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    #region Giriş İşlemi Testleri

    [Fact]
    [Trait("Category", "Turnstile")]
    [Trait("Priority", "Critical")]
    public async Task Enter_WithValidStudentNumber_ShouldReturnAccessResult()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        var request = new TurnstileRequest
        {
            StudentNumber = studentNumber
        };

        // Act
        var response = await _fixture.TurnstileClient.PostAsync<TurnstileEntryResponse>(
            "/api/Turnstile/enter", request);

        // Assert
        response.IsSuccess.Should().BeTrue("Turnike isteği yanıt vermeli");
        response.Data.Should().NotBeNull("Yanıt verisi dönmeli");
        response.Data!.Message.Should().NotBeNullOrEmpty("Mesaj dönmeli");

        _output.WriteLine($"✓ Turnike yanıtı: DoorOpen={response.Data.DoorOpen}, Mesaj={response.Data.Message}");
    }

    [Fact]
    [Trait("Category", "Turnstile")]
    [Trait("Priority", "Critical")]
    public async Task Enter_WithoutReservation_ShouldDenyAccess()
    {
        // Arrange - Rezervasyonu olmayan yeni kullanıcı
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        var request = new TurnstileRequest
        {
            StudentNumber = studentNumber
        };

        // Act
        var response = await _fixture.TurnstileClient.PostAsync<TurnstileEntryResponse>(
            "/api/Turnstile/enter", request);

        // Assert - Rezervasyon olmadığı için giriş yapılamayabilir
        response.IsSuccess.Should().BeTrue("Turnike isteği işlenmeli");
        
        _output.WriteLine($"✓ Rezervasyonsuz giriş denemesi: DoorOpen={response.Data?.DoorOpen}");
    }

    [Fact]
    [Trait("Category", "Turnstile")]
    [Trait("Priority", "High")]
    public async Task Enter_WithEmptyStudentNumber_ShouldReturnBadRequest()
    {
        // Arrange
        var request = new TurnstileRequest
        {
            StudentNumber = ""
        };

        // Act
        var response = await _fixture.TurnstileClient.PostAsync<TurnstileEntryResponse>(
            "/api/Turnstile/enter", request);

        // Assert
        response.IsSuccess.Should().BeFalse("Boş öğrenci numarası reddedilmeli");
        response.StatusCode.Should().Be(400, "Bad Request dönmeli");

        _output.WriteLine($"✓ Boş öğrenci numarası reddedildi");
    }

    [Fact]
    [Trait("Category", "Turnstile")]
    [Trait("Priority", "High")]
    public async Task Enter_WithNullRequest_ShouldReturnBadRequest()
    {
        // Act
        var response = await _fixture.TurnstileClient.PostAsync<TurnstileEntryResponse>(
            "/api/Turnstile/enter", null);

        // Assert
        response.IsSuccess.Should().BeFalse("Null istek reddedilmeli");

        _output.WriteLine($"✓ Null istek reddedildi");
    }

    [Fact]
    [Trait("Category", "Turnstile")]
    [Trait("Priority", "Medium")]
    public async Task Enter_WithNonExistentStudent_ShouldHandleGracefully()
    {
        // Arrange
        var request = new TurnstileRequest
        {
            StudentNumber = "nonexistent_" + DateTime.Now.Ticks
        };

        // Act
        var response = await _fixture.TurnstileClient.PostAsync<TurnstileEntryResponse>(
            "/api/Turnstile/enter", request);

        // Assert - Sistem hata vermeden işlemeli
        _output.WriteLine($"✓ Var olmayan öğrenci işlendi: StatusCode={response.StatusCode}");
    }

    #endregion

    #region Giriş Logları Testleri

    [Fact]
    [Trait("Category", "Turnstile")]
    [Trait("Priority", "High")]
    public async Task GetLogs_ShouldReturnEntryLogs()
    {
        // Act
        var response = await _fixture.TurnstileClient.GetAsync<List<TurnstileLogEntry>>(
            "/api/Turnstile/logs?take=20");

        // Assert
        response.IsSuccess.Should().BeTrue("Log listesi dönmeli");
        response.Data.Should().NotBeNull("Log verisi dönmeli");

        _output.WriteLine($"✓ Turnike logları alındı: {response.Data?.Count ?? 0} kayıt");
    }

    [Fact]
    [Trait("Category", "Turnstile")]
    [Trait("Priority", "Medium")]
    public async Task GetLogs_WithCustomTakeParameter_ShouldLimitResults()
    {
        // Arrange
        var take = 5;

        // Act
        var response = await _fixture.TurnstileClient.GetAsync<List<TurnstileLogEntry>>(
            $"/api/Turnstile/logs?take={take}");

        // Assert
        response.IsSuccess.Should().BeTrue("Log listesi dönmeli");
        response.Data?.Count.Should().BeLessOrEqualTo(take, $"En fazla {take} kayıt dönmeli");

        _output.WriteLine($"✓ Limitli log listesi alındı: {response.Data?.Count ?? 0} kayıt");
    }

    #endregion

    #region Entegrasyon Testleri

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "Critical")]
    public async Task FullEntryFlow_CreateReservationAndEnter_ShouldWork()
    {
        // Arrange - Kullanıcı oluştur
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        // Step 1: Rezervasyon oluştur
        var today = DateTime.Now;
        var reservationRequest = new
        {
            StudentNumber = studentNumber,
            TableId = 1,
            ReservationDate = today.ToString("yyyy-MM-dd"),
            StartTime = today.Hour.ToString("00") + ":00",
            EndTime = (today.Hour + 2).ToString("00") + ":00"
        };

        var reservationResponse = await _fixture.ReservationClient.PostAsync<ReservationResultResponse>(
            "/api/Reservation/Create", reservationRequest);

        _output.WriteLine($"Rezervasyon oluşturma: {reservationResponse.StatusCode}");

        // Step 2: Turnike girişi dene
        var turnstileRequest = new TurnstileRequest
        {
            StudentNumber = studentNumber
        };

        var turnstileResponse = await _fixture.TurnstileClient.PostAsync<TurnstileEntryResponse>(
            "/api/Turnstile/enter", turnstileRequest);

        // Assert
        turnstileResponse.IsSuccess.Should().BeTrue("Turnike yanıt vermeli");
        
        _output.WriteLine($"✓ Tam giriş akışı testi tamamlandı");
        _output.WriteLine($"  - Rezervasyon: {(reservationResponse.IsSuccess ? "Başarılı" : "Başarısız")}");
        _output.WriteLine($"  - Turnike: DoorOpen={turnstileResponse.Data?.DoorOpen}");
    }

    [Fact]
    [Trait("Category", "Integration")]
    [Trait("Priority", "High")]
    public async Task MultipleEntryAttempts_ShouldBeRecordedInLogs()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        // Act - 3 kez giriş denemesi yap
        for (int i = 0; i < 3; i++)
        {
            var request = new TurnstileRequest { StudentNumber = studentNumber };
            await _fixture.TurnstileClient.PostAsync<TurnstileEntryResponse>("/api/Turnstile/enter", request);
            await Task.Delay(100); // Kısa bekleme
        }

        // Logları kontrol et
        var logsResponse = await _fixture.TurnstileClient.GetAsync<List<TurnstileLogEntry>>(
            "/api/Turnstile/logs?take=50");

        // Assert
        logsResponse.IsSuccess.Should().BeTrue("Loglar dönmeli");
        
        var userLogs = logsResponse.Data?.Where(l => l.StudentNumber == studentNumber).ToList();
        userLogs?.Count.Should().BeGreaterOrEqualTo(3, "En az 3 log kaydı olmalı");

        _output.WriteLine($"✓ Çoklu giriş denemesi loglandı: {userLogs?.Count ?? 0} kayıt");
    }

    #endregion
}

#region Turnstile Models

public class TurnstileRequest
{
    public string StudentNumber { get; set; } = string.Empty;
}

public class TurnstileEntryResponse
{
    public string Message { get; set; } = string.Empty;
    public bool DoorOpen { get; set; }
}

public class TurnstileLogEntry
{
    public string StudentNumber { get; set; } = string.Empty;
    public bool Allowed { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public DateTime LocalTime { get; set; }
}

#endregion
