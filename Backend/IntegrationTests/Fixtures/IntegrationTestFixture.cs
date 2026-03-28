using IntegrationTests.Helpers;
using IntegrationTests.Models;
using Xunit;

namespace IntegrationTests.Fixtures;

/// <summary>
/// Tüm integration testleri için temel fixture sınıfı
/// </summary>
public class IntegrationTestFixture : IAsyncLifetime
{
    public ApiClient IdentityClient { get; private set; } = null!;
    public ApiClient ReservationClient { get; private set; } = null!;
    public ApiClient TurnstileClient { get; private set; } = null!;
    public ApiClient FeedbackClient { get; private set; } = null!;
    public ApiClient GatewayClient { get; private set; } = null!;

    public string? CurrentUserId { get; private set; }
    public string? CurrentToken { get; private set; }

    public async Task InitializeAsync()
    {
        // API Client'ları oluştur
        IdentityClient = new ApiClient(TestConfiguration.IdentityServiceUrl);
        ReservationClient = new ApiClient(TestConfiguration.ReservationServiceUrl);
        TurnstileClient = new ApiClient(TestConfiguration.TurnstileServiceUrl);
        FeedbackClient = new ApiClient(TestConfiguration.FeedbackServiceUrl);
        GatewayClient = new ApiClient(TestConfiguration.GatewayUrl);

        // Servislerin çalıştığını kontrol et
        await WaitForServicesAsync();
    }

    public async Task DisposeAsync()
    {
        IdentityClient?.Dispose();
        ReservationClient?.Dispose();
        TurnstileClient?.Dispose();
        FeedbackClient?.Dispose();
        GatewayClient?.Dispose();
        await Task.CompletedTask;
    }

    /// <summary>
    /// Test kullanıcısı ile giriş yap
    /// </summary>
    public async Task<AuthResponse?> LoginTestUserAsync(string? studentNumber = null, string? password = null)
    {
        var loginRequest = new LoginRequest
        {
            StudentNumber = studentNumber ?? TestConfiguration.TestUser.StudentNumber,
            Password = password ?? TestConfiguration.TestUser.Password
        };

        var response = await IdentityClient.PostAsync<AuthResponse>("/api/Auth/login", loginRequest);

        if (response.IsSuccess && response.Data != null)
        {
            CurrentToken = response.Data.AccessToken;
            CurrentUserId = response.Data.StudentNumber;

            // Tüm client'lara token'ı ata
            SetAuthTokenForAllClients(response.Data.AccessToken);
            return response.Data;
        }

        return null;
    }

    /// <summary>
    /// Test kullanıcısı oluştur ve giriş yap
    /// </summary>
    public async Task<AuthResponse?> RegisterTestUserAsync(string studentNumber)
    {
        var registerRequest = new RegisterRequest
        {
            StudentNumber = studentNumber,
            FullName = $"Test User {studentNumber}",
            Email = $"{studentNumber}@test.com",
            Password = TestConfiguration.TestUser.Password,
            AcademicLevel = TestConfiguration.TestUser.AcademicLevel
        };

        // Önce kayıt ol
        var registerResponse = await IdentityClient.PostAsync<RegisterResponse>("/api/Auth/register", registerRequest);

        if (!registerResponse.IsSuccess)
        {
            return null;
        }

        // Sonra login yap ve token al
        return await LoginTestUserAsync(studentNumber, TestConfiguration.TestUser.Password);
    }

    /// <summary>
    /// Rastgele öğrenci numarası oluştur (max 20 karakter - Feedback API limiti)
    /// </summary>
    public static string GenerateStudentNumber()
    {
        // Feedback API StudentNumber için max 20 karakter kabul ediyor
        return $"t{Random.Shared.Next(100000, 999999)}";
    }

    /// <summary>
    /// Tüm client'lara auth token ata
    /// </summary>
    private void SetAuthTokenForAllClients(string token)
    {
        IdentityClient.SetAuthToken(token);
        ReservationClient.SetAuthToken(token);
        TurnstileClient.SetAuthToken(token);
        FeedbackClient.SetAuthToken(token);
        GatewayClient.SetAuthToken(token);
    }

    /// <summary>
    /// Auth bilgilerini temizle
    /// </summary>
    public void ClearAuth()
    {
        CurrentToken = null;
        CurrentUserId = null;
        IdentityClient.ClearAuth();
        ReservationClient.ClearAuth();
        TurnstileClient.ClearAuth();
        FeedbackClient.ClearAuth();
        GatewayClient.ClearAuth();
    }

    /// <summary>
    /// Servislerin hazır olmasını bekle
    /// </summary>
    private async Task WaitForServicesAsync()
    {
        var maxAttempts = 10;
        var services = new[]
        {
            // Health endpoint yerine gerçek API endpoint'lerini kontrol et
            (TestConfiguration.IdentityServiceUrl + "/api/Auth/login", "Identity"),
            (TestConfiguration.ReservationServiceUrl + "/api/Reservation/Faculties", "Reservation"),
        };

        foreach (var (url, name) in services)
        {
            for (int i = 0; i < maxAttempts; i++)
            {
                try
                {
                    using var client = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
                    // POST veya GET yaparak servisin çalıştığını kontrol et
                    var response = await client.GetAsync(url);
                    // 404, 401 bile olsa servis çalışıyor demektir
                    break;
                }
                catch
                {
                    // Servis henüz hazır değil
                }

                if (i == maxAttempts - 1)
                {
                    Console.WriteLine($"⚠️ {name} servisi yanıt vermiyor, testler başarısız olabilir.");
                }

                await Task.Delay(500);
            }
        }
    }
}

/// <summary>
/// Test Collection tanımı
/// </summary>
[CollectionDefinition("Integration Tests")]
public class IntegrationTestCollection : ICollectionFixture<IntegrationTestFixture>
{
}
