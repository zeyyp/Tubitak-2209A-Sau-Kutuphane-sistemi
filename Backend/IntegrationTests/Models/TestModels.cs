namespace IntegrationTests.Models;

#region Authentication Models

public class LoginRequest
{
    public string StudentNumber { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class RegisterRequest
{
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string AcademicLevel { get; set; } = string.Empty;
    public string? Faculty { get; set; }
    public string? Department { get; set; }
}

public class RegisterResponse
{
    public string Message { get; set; } = string.Empty;
}

/// <summary>
/// Login API yanıt modeli - Gerçek API yanıtına göre
/// </summary>
public class AuthResponse
{
    public string Message { get; set; } = string.Empty;
    public string AccessToken { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
    public DateTime AccessTokenExpiresAt { get; set; }
    public DateTime RefreshTokenExpiresAt { get; set; }
    public string StudentNumber { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string AcademicLevel { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
}

public class UserInfo
{
    public string Id { get; set; } = string.Empty;
    public string StudentNumber { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string AcademicLevel { get; set; } = string.Empty;
    public int PriorityScore { get; set; }
    public string? ActiveReservationId { get; set; }
}

public class RefreshTokenRequest
{
    public string Token { get; set; } = string.Empty;
    public string RefreshToken { get; set; } = string.Empty;
}

#endregion

#region Reservation Models

public class CreateReservationRequest
{
    public string TableId { get; set; } = string.Empty;
    public string StartTime { get; set; } = string.Empty;
    public string EndTime { get; set; } = string.Empty;
}

public class ReservationResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string TableId { get; set; } = string.Empty;
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class TableInfo
{
    public string Id { get; set; } = string.Empty;
    public string TableNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Floor { get; set; } = string.Empty;
    public string Section { get; set; } = string.Empty;
}

public class AvailabilityResponse
{
    public List<TableInfo> AvailableTables { get; set; } = new();
    public List<TimeSlot> AvailableSlots { get; set; } = new();
}

public class TimeSlot
{
    public DateTime StartTime { get; set; }
    public DateTime EndTime { get; set; }
}

#endregion

#region Turnstile Models

public class TurnstileEntryRequest
{
    public string StudentNumber { get; set; } = string.Empty;
    public string TurnstileId { get; set; } = string.Empty;
}

public class TurnstileResponse
{
    public bool Allowed { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? EntryId { get; set; }
    public DateTime? Timestamp { get; set; }
}

public class ActiveEntryResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public DateTime EntryTime { get; set; }
    public string TurnstileId { get; set; } = string.Empty;
}

#endregion

#region Feedback Models

public class CreateFeedbackRequest
{
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Category { get; set; } = string.Empty;
}

public class FeedbackResponse
{
    public string Id { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public int Rating { get; set; }
    public string Category { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

#endregion

#region Common Models

public class ErrorResponse
{
    public string Message { get; set; } = string.Empty;
    public string? Error { get; set; }
    public Dictionary<string, string[]>? Errors { get; set; }
}

public class PaginatedResponse<T>
{
    public List<T> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
}

#endregion
