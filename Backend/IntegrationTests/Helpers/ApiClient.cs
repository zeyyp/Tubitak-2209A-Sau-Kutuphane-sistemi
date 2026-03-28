using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace IntegrationTests.Helpers;

/// <summary>
/// API istekleri için yardımcı sınıf
/// </summary>
public class ApiClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private string? _accessToken;
    private string? _refreshToken;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public ApiClient(string baseUrl)
    {
        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl),
            Timeout = TestConfiguration.Timeouts.HttpRequest
        };
    }

    public void SetAuthToken(string token)
    {
        _accessToken = token;
        _httpClient.DefaultRequestHeaders.Authorization = 
            new AuthenticationHeaderValue("Bearer", token);
    }

    public void SetRefreshToken(string token)
    {
        _refreshToken = token;
    }

    public void ClearAuth()
    {
        _accessToken = null;
        _refreshToken = null;
        _httpClient.DefaultRequestHeaders.Authorization = null;
    }

    public string? GetAccessToken() => _accessToken;
    public string? GetRefreshToken() => _refreshToken;

    public async Task<ApiResponse<T>> GetAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.GetAsync(endpoint);
            return await ParseResponse<T>(response);
        }
        catch (Exception ex)
        {
            return ApiResponse<T>.Failure(ex.Message);
        }
    }

    public async Task<ApiResponse<T>> PostAsync<T>(string endpoint, object? data = null)
    {
        try
        {
            var content = data != null 
                ? new StringContent(JsonSerializer.Serialize(data, JsonOptions), Encoding.UTF8, "application/json")
                : null;
            
            var response = await _httpClient.PostAsync(endpoint, content);
            return await ParseResponse<T>(response);
        }
        catch (Exception ex)
        {
            return ApiResponse<T>.Failure(ex.Message);
        }
    }

    public async Task<ApiResponse<T>> PutAsync<T>(string endpoint, object? data = null)
    {
        try
        {
            var content = data != null 
                ? new StringContent(JsonSerializer.Serialize(data, JsonOptions), Encoding.UTF8, "application/json")
                : null;
            
            var response = await _httpClient.PutAsync(endpoint, content);
            return await ParseResponse<T>(response);
        }
        catch (Exception ex)
        {
            return ApiResponse<T>.Failure(ex.Message);
        }
    }

    public async Task<ApiResponse<T>> DeleteAsync<T>(string endpoint)
    {
        try
        {
            var response = await _httpClient.DeleteAsync(endpoint);
            return await ParseResponse<T>(response);
        }
        catch (Exception ex)
        {
            return ApiResponse<T>.Failure(ex.Message);
        }
    }

    private static async Task<ApiResponse<T>> ParseResponse<T>(HttpResponseMessage response)
    {
        var content = await response.Content.ReadAsStringAsync();
        
        if (response.IsSuccessStatusCode)
        {
            if (string.IsNullOrEmpty(content))
            {
                return ApiResponse<T>.Success(default!, (int)response.StatusCode);
            }

            try
            {
                var data = JsonSerializer.Deserialize<T>(content, JsonOptions);
                return ApiResponse<T>.Success(data!, (int)response.StatusCode);
            }
            catch
            {
                return ApiResponse<T>.Success(default!, (int)response.StatusCode, content);
            }
        }
        
        return ApiResponse<T>.Failure(content, (int)response.StatusCode);
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}

/// <summary>
/// API yanıt modeli
/// </summary>
public class ApiResponse<T>
{
    public bool IsSuccess { get; init; }
    public T? Data { get; init; }
    public string? ErrorMessage { get; init; }
    public int StatusCode { get; init; }
    public string? RawContent { get; init; }

    public static ApiResponse<T> Success(T data, int statusCode, string? rawContent = null)
        => new() { IsSuccess = true, Data = data, StatusCode = statusCode, RawContent = rawContent };

    public static ApiResponse<T> Failure(string error, int statusCode = 0)
        => new() { IsSuccess = false, ErrorMessage = error, StatusCode = statusCode };
}
