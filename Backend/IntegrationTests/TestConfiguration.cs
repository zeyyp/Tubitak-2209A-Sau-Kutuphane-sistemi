namespace IntegrationTests;

/// <summary>
/// Test ortamı yapılandırması
/// TÜBİTAK 2209-A Projesi - SAÜ Kütüphane Rezervasyon Sistemi
/// </summary>
public static class TestConfiguration
{
    // API Base URLs (Docker Compose'dan)
    public const string GatewayUrl = "http://localhost:5010";
    public const string IdentityServiceUrl = "http://localhost:5001";
    public const string ReservationServiceUrl = "http://localhost:5002";
    public const string TurnstileServiceUrl = "http://localhost:5003";
    public const string FeedbackServiceUrl = "http://localhost:5004";

    // Test Kullanıcı Bilgileri
    public static class TestUser
    {
        public const string StudentNumber = "test_integration";
        public const string Password = "Test123!";
        public const string Email = "test_integration@test.com";
        public const string FullName = "Integration Test User";
        public const string AcademicLevel = "Lisans";
    }

    // Zaman Aşımı Değerleri
    public static class Timeouts
    {
        public static readonly TimeSpan HttpRequest = TimeSpan.FromSeconds(30);
        public static readonly TimeSpan LongRunning = TimeSpan.FromMinutes(2);
    }
}
