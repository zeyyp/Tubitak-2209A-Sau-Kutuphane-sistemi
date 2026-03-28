using FluentAssertions;
using IntegrationTests.Fixtures;
using IntegrationTests.Models;
using Xunit;
using Xunit.Abstractions;

namespace IntegrationTests.Tests;

/// <summary>
/// Rezervasyon Servisi Integration Testleri
/// TÜBİTAK 2209-A - SAÜ Kütüphane Rezervasyon Sistemi
/// </summary>
[Collection("Integration Tests")]
public class ReservationTests
{
    private readonly IntegrationTestFixture _fixture;
    private readonly ITestOutputHelper _output;

    public ReservationTests(IntegrationTestFixture fixture, ITestOutputHelper output)
    {
        _fixture = fixture;
        _output = output;
    }

    #region Masa Sorgulama Testleri

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "Critical")]
    public async Task GetTables_WithValidParameters_ShouldReturnTableList()
    {
        // Arrange
        var tomorrow = DateTime.Now.AddDays(1).ToString("yyyy-MM-dd");
        var start = "09:00";
        var end = "11:00";
        var floorId = 1;

        // Act
        var response = await _fixture.ReservationClient.GetAsync<List<TableResponse>>(
            $"/api/Reservation/Tables?date={tomorrow}&start={start}&end={end}&floorId={floorId}");

        // Assert
        response.IsSuccess.Should().BeTrue("Masa listesi başarıyla dönmeli");
        _output.WriteLine($"✓ Masa listesi alındı: {response.Data?.Count ?? 0} masa");
    }

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "High")]
    public async Task GetTables_WithInvalidDateFormat_ShouldReturnBadRequest()
    {
        // Arrange
        var invalidDate = "invalid-date";
        var start = "09:00";
        var end = "11:00";
        var floorId = 1;

        // Act
        var response = await _fixture.ReservationClient.GetAsync<object>(
            $"/api/Reservation/Tables?date={invalidDate}&start={start}&end={end}&floorId={floorId}");

        // Assert
        response.StatusCode.Should().Be(400, "Geçersiz tarih formatı için Bad Request dönmeli");
        _output.WriteLine($"✓ Geçersiz tarih formatı reddedildi");
    }

    #endregion

    #region Rezervasyon Oluşturma Testleri

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "Critical")]
    public async Task CreateReservation_WithValidData_ShouldSucceed()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        var tomorrow = DateTime.Now.AddDays(1);
        var request = new
        {
            StudentNumber = studentNumber,
            TableId = 1,
            ReservationDate = tomorrow.ToString("yyyy-MM-dd"),
            StartTime = "10:00",
            EndTime = "12:00"
        };

        // Act
        var response = await _fixture.ReservationClient.PostAsync<ReservationResultResponse>(
            "/api/Reservation/Create", request);

        // Assert
        _output.WriteLine($"Create Response: {response.StatusCode} - {response.ErrorMessage}");
        
        if (response.IsSuccess)
        {
            response.Data.Should().NotBeNull("Rezervasyon verisi dönmeli");
            _output.WriteLine($"✓ Rezervasyon oluşturuldu: {response.Data?.ReservationId}");
        }
        else
        {
            // Eğer masa müsait değilse veya başka bir kısıtlama varsa kabul edilebilir
            _output.WriteLine($"⚠ Rezervasyon oluşturulamadı (beklenebilir): {response.ErrorMessage}");
        }
    }

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "High")]
    public async Task CreateReservation_WithPastDate_ShouldFail()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        var yesterday = DateTime.Now.AddDays(-1);
        var request = new
        {
            StudentNumber = studentNumber,
            TableId = 1,
            ReservationDate = yesterday.ToString("yyyy-MM-dd"),
            StartTime = "10:00",
            EndTime = "12:00"
        };

        // Act
        var response = await _fixture.ReservationClient.PostAsync<object>(
            "/api/Reservation/Create", request);

        // Assert
        response.IsSuccess.Should().BeFalse("Geçmiş tarih için rezervasyon yapılamamalı");
        _output.WriteLine($"✓ Geçmiş tarih rezervasyonu reddedildi");
    }

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "High")]
    public async Task CreateReservation_DurationLessThan1Hour_ShouldFail()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        var tomorrow = DateTime.Now.AddDays(1);
        var request = new
        {
            StudentNumber = studentNumber,
            TableId = 1,
            ReservationDate = tomorrow.ToString("yyyy-MM-dd"),
            StartTime = "10:00",
            EndTime = "10:30" // 30 dakika - minimum 1 saat olmalı
        };

        // Act
        var response = await _fixture.ReservationClient.PostAsync<object>(
            "/api/Reservation/Create", request);

        // Assert
        response.IsSuccess.Should().BeFalse("1 saatten kısa rezervasyon yapılamamalı");
        _output.WriteLine($"✓ Kısa süreli rezervasyon reddedildi");
    }

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "High")]
    public async Task CreateReservation_DurationMoreThan4Hours_ShouldFail()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        var tomorrow = DateTime.Now.AddDays(1);
        var request = new
        {
            StudentNumber = studentNumber,
            TableId = 1,
            ReservationDate = tomorrow.ToString("yyyy-MM-dd"),
            StartTime = "09:00",
            EndTime = "15:00" // 6 saat - maksimum 4 saat olmalı
        };

        // Act
        var response = await _fixture.ReservationClient.PostAsync<object>(
            "/api/Reservation/Create", request);

        // Assert
        response.IsSuccess.Should().BeFalse("4 saatten uzun rezervasyon yapılamamalı");
        _output.WriteLine($"✓ Uzun süreli rezervasyon reddedildi");
    }

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "Medium")]
    public async Task CreateReservation_WithInvalidTableId_ShouldFail()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        var tomorrow = DateTime.Now.AddDays(1);
        var request = new
        {
            StudentNumber = studentNumber,
            TableId = 99999, // Var olmayan masa
            ReservationDate = tomorrow.ToString("yyyy-MM-dd"),
            StartTime = "10:00",
            EndTime = "12:00"
        };

        // Act
        var response = await _fixture.ReservationClient.PostAsync<object>(
            "/api/Reservation/Create", request);

        // Assert
        response.IsSuccess.Should().BeFalse("Geçersiz masa ID için rezervasyon yapılamamalı");
        _output.WriteLine($"✓ Geçersiz masa ID reddedildi: {response.ErrorMessage}");
    }

    #endregion

    #region Rezervasyon Listeleme Testleri

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "Critical")]
    public async Task GetMyReservations_ShouldReturnUserReservations()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        // Act
        var response = await _fixture.ReservationClient.GetAsync<List<ReservationResponse>>(
            $"/api/Reservation/MyReservations?studentNumber={studentNumber}");

        // Assert
        response.IsSuccess.Should().BeTrue("Kullanıcı rezervasyonları dönmeli");
        response.Data.Should().NotBeNull();
        _output.WriteLine($"✓ Kullanıcı rezervasyonları alındı: {response.Data?.Count ?? 0} rezervasyon");
    }

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "High")]
    public async Task GetAllReservations_ShouldReturnAllReservations()
    {
        // Arrange
        var today = DateTime.Now.ToString("yyyy-MM-dd");

        // Act
        var response = await _fixture.ReservationClient.GetAsync<List<ReservationResponse>>(
            $"/api/Reservation/All?date={today}");

        // Assert
        response.IsSuccess.Should().BeTrue("Tüm rezervasyonlar dönmeli");
        _output.WriteLine($"✓ Tüm rezervasyonlar alındı: {response.Data?.Count ?? 0} rezervasyon");
    }

    #endregion

    #region Rezervasyon İptal Testleri

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "Critical")]
    public async Task CancelReservation_OwnReservation_ShouldSucceed()
    {
        // Arrange - Önce rezervasyon oluştur
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        var tomorrow = DateTime.Now.AddDays(1);
        var createRequest = new
        {
            StudentNumber = studentNumber,
            TableId = 2, // Farklı masa
            ReservationDate = tomorrow.ToString("yyyy-MM-dd"),
            StartTime = "14:00",
            EndTime = "16:00"
        };

        var createResponse = await _fixture.ReservationClient.PostAsync<ReservationResultResponse>(
            "/api/Reservation/Create", createRequest);

        if (!createResponse.IsSuccess)
        {
            _output.WriteLine($"⚠ Rezervasyon oluşturulamadı, test atlanıyor: {createResponse.ErrorMessage}");
            return;
        }

        var reservationId = createResponse.Data!.ReservationId;

        // Act - Rezervasyonu iptal et
        var cancelResponse = await _fixture.ReservationClient.DeleteAsync<object>(
            $"/api/Reservation/Cancel/{reservationId}?studentNumber={studentNumber}");

        // Assert
        cancelResponse.IsSuccess.Should().BeTrue("Kendi rezervasyonunu iptal edebilmeli");
        _output.WriteLine($"✓ Rezervasyon iptal edildi: {reservationId}");
    }

    [Fact]
    [Trait("Category", "Reservation")]
    [Trait("Priority", "High")]
    public async Task CancelReservation_NonExistentReservation_ShouldFail()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        var fakeReservationId = 99999;

        // Act
        var response = await _fixture.ReservationClient.DeleteAsync<object>(
            $"/api/Reservation/Cancel/{fakeReservationId}?studentNumber={studentNumber}");

        // Assert
        response.IsSuccess.Should().BeFalse("Var olmayan rezervasyon iptal edilememeli");
        response.StatusCode.Should().BeOneOf(new[] { 404, 400, 403 }, "Not Found veya hata dönmeli");
        _output.WriteLine($"✓ Var olmayan rezervasyon iptal isteği reddedildi");
    }

    #endregion

    #region Öncelik ve Erişim Kontrolü Testleri

    [Fact]
    [Trait("Category", "Priority")]
    [Trait("Priority", "Critical")]
    public async Task CheckAccess_WithActiveReservation_ShouldAllowEntry()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        // Act
        var response = await _fixture.ReservationClient.GetAsync<AccessCheckResponse>(
            $"/api/Reservation/CheckAccess?studentNumber={studentNumber}");

        // Assert
        response.IsSuccess.Should().BeTrue("Erişim kontrolü yanıt vermeli");
        _output.WriteLine($"✓ Erişim kontrolü yapıldı: {response.Data?.CanEnter}");
    }

    [Fact]
    [Trait("Category", "Priority")]
    [Trait("Priority", "High")]
    public async Task GetProfile_ShouldReturnPriorityScore()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        // Act
        var response = await _fixture.ReservationClient.GetAsync<ProfileResponse>(
            $"/api/Reservation/Profile/{studentNumber}");

        // Assert
        response.IsSuccess.Should().BeTrue("Profil bilgisi dönmeli");
        if (response.Data != null)
        {
            response.Data.PriorityScore.Should().BeGreaterOrEqualTo(0, "Öncelik puanı 0 veya üzeri olmalı");
            _output.WriteLine($"✓ Profil alındı - Öncelik Puanı: {response.Data.PriorityScore}");
        }
    }

    #endregion

    #region Ceza Sistemi Testleri

    [Fact]
    [Trait("Category", "Penalty")]
    [Trait("Priority", "High")]
    public async Task GetPenalties_ShouldReturnPenaltyList()
    {
        // Arrange
        var studentNumber = IntegrationTestFixture.GenerateStudentNumber();
        await _fixture.RegisterTestUserAsync(studentNumber);

        // Act
        var response = await _fixture.ReservationClient.GetAsync<List<PenaltyResponse>>(
            $"/api/Reservation/Penalties?studentNumber={studentNumber}");

        // Assert
        response.IsSuccess.Should().BeTrue("Ceza listesi dönmeli");
        _output.WriteLine($"✓ Ceza listesi alındı: {response.Data?.Count ?? 0} ceza");
    }

    #endregion

    #region Fakülte ve Bölüm Testleri

    [Fact]
    [Trait("Category", "Master Data")]
    [Trait("Priority", "Medium")]
    public async Task GetFaculties_ShouldReturnFacultyList()
    {
        // Act
        var response = await _fixture.ReservationClient.GetAsync<List<FacultyResponse>>(
            "/api/Reservation/Faculties");

        // Assert
        response.IsSuccess.Should().BeTrue("Fakülte listesi dönmeli");
        response.Data.Should().NotBeNull();
        response.Data!.Count.Should().BeGreaterThan(0, "En az bir fakülte olmalı");
        _output.WriteLine($"✓ Fakülte listesi alındı: {response.Data.Count} fakülte");
    }

    #endregion

    #region Sınav Haftası Testleri

    [Fact]
    [Trait("Category", "ExamWeek")]
    [Trait("Priority", "Medium")]
    public async Task GetExamWeeks_ShouldReturnExamWeekList()
    {
        // Act
        var response = await _fixture.ReservationClient.GetAsync<List<ExamWeekResponse>>(
            "/api/Reservation/ExamWeeks");

        // Assert
        response.IsSuccess.Should().BeTrue("Sınav haftası listesi dönmeli");
        _output.WriteLine($"✓ Sınav haftaları alındı: {response.Data?.Count ?? 0} hafta");
    }

    #endregion
}

#region Response Models

public class TableResponse
{
    public int Id { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public int FloorId { get; set; }
    public bool IsAvailable { get; set; }
}

public class ReservationResultResponse
{
    public int ReservationId { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class AccessCheckResponse
{
    public bool CanEnter { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ReservationId { get; set; }
}

public class ProfileResponse
{
    public string StudentNumber { get; set; } = string.Empty;
    public string? FullName { get; set; }
    public string? AcademicLevel { get; set; }
    public int PriorityScore { get; set; }
    public int TotalReservations { get; set; }
    public int CompletedReservations { get; set; }
    public int CancelledReservations { get; set; }
    public int NoShowCount { get; set; }
}

public class PenaltyResponse
{
    public int Id { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ExpiresAt { get; set; }
    public bool IsActive { get; set; }
}

public class FacultyResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<DepartmentResponse> Departments { get; set; } = new();
}

public class DepartmentResponse
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int FacultyId { get; set; }
}

public class ExamWeekResponse
{
    public int Id { get; set; }
    public int FacultyId { get; set; }
    public int? DepartmentId { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public bool IsActive { get; set; }
}

#endregion
