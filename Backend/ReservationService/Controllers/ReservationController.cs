using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Services;
using Shared.Events;

namespace ReservationService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class ReservationController : ControllerBase
    {
        private readonly ReservationDbContext _context;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ReservationController> _logger;
        private readonly RabbitMQPublisher _publisher;
        private readonly PriorityService _priorityService;

        private const int EarlyToleranceMinutes = 5;  // Başlangıçtan 5dk önce giriş yapılabilir
        private const int EntryGracePeriodMinutes = 15; // Başlangıçtan 15dk sonrasına kadar giriş yapılabilir, sonra ceza
        private const int PenaltyThreshold = 3;
        private const int BanDurationDays = 7;
        private static readonly TimeSpan MinReservationDuration = TimeSpan.FromHours(1);
        private static readonly TimeSpan MaxReservationDuration = TimeSpan.FromHours(4);

        private static readonly StringComparer StudentTypeComparer = StringComparer.OrdinalIgnoreCase;

        private static readonly Dictionary<string, StudentTypeRule> StudentTypeRules = new(StudentTypeComparer)
        {
            // Herkes için eşit: 2 aktif rezervasyon, sadece bugün + yarın (1 gün ileri)
            ["lisans"] = new StudentTypeRule(Priority: 1, MaxAdvanceDays: 1, MaxActiveReservations: 2),
            ["yukseklisans"] = new StudentTypeRule(Priority: 2, MaxAdvanceDays: 1, MaxActiveReservations: 2),
            ["yükseklisans"] = new StudentTypeRule(Priority: 2, MaxAdvanceDays: 1, MaxActiveReservations: 2),
            ["doktora"] = new StudentTypeRule(Priority: 3, MaxAdvanceDays: 1, MaxActiveReservations: 2),
            // Admin için kısıtları esnetiyoruz
            ["admin"] = new StudentTypeRule(Priority: 99, MaxAdvanceDays: 30, MaxActiveReservations: 10)
        };

        private static readonly Dictionary<string, string> StudentTypeCanonicalNames = new(StudentTypeComparer)
        {
            ["lisans"] = "Lisans",
            ["yukseklisans"] = "YüksekLisans",
            ["yükseklisans"] = "YüksekLisans",
            ["doktora"] = "Doktora",
            ["admin"] = "Admin"
        };

        // Scoring sistemi için sabitler
        private const int ScoreDoktora = 300;
        private const int ScoreYuksekLisans = 200;
        private const int ScoreLisans = 100;
        private const int ScoreLisansExamBonus = 50; // Sadece Lisans için sınav haftası bonusu

        public ReservationController(
            ReservationDbContext context, 
            IHttpClientFactory httpClientFactory, 
            ILogger<ReservationController> logger, 
            RabbitMQPublisher publisher,
            PriorityService priorityService)
        {
            _context = context;
            _httpClientFactory = httpClientFactory;
            _publisher = publisher;
            _logger = logger;
            _priorityService = priorityService;
        }

        private string? GetCurrentStudentNumber()
        {
            // JWT devre dışıyken claim olmayabilir; eski davranışa yakın kalmak için
            // varsa claim'i kullan, yoksa null döndür.
            return User.FindFirst("studentNumber")?.Value;
        }

        // Geçici olarak tüm istekleri admin / service gibi kabul ediyoruz
        // ki JWT zorunluluğu kalktığında eski UI akışı çalışsın.
        private bool IsAdmin => true;

        private bool IsService => true;

        private record StudentTypeRule(int Priority, int MaxAdvanceDays, int MaxActiveReservations);

        private sealed class IdentityProfileDto
        {
            public string StudentNumber { get; set; } = string.Empty;
            public string? AcademicLevel { get; set; }
            public string? Role { get; set; }
            public string? FullName { get; set; }
            public string? Email { get; set; }
        }

        [HttpGet("Tables")]
        public async Task<IActionResult> GetTables(string date, string start, string end, int floorId)
        {
            if (!DateOnly.TryParse(date, out var rDate) || 
                !TimeOnly.TryParse(start, out var rStart) || 
                !TimeOnly.TryParse(end, out var rEnd))
            {
                return BadRequest("Geçersiz tarih/saat formatı.");
            }

            var tables = await _context.Tables
                .Where(t => t.FloorId == floorId)
                .ToListAsync();

            var result = new List<object>();

            foreach (var table in tables)
            {
                // Check if table is occupied in the requested time slot
                var isOccupied = await _context.Reservations.AnyAsync(r => 
                    r.TableId == table.Id && 
                    r.ReservationDate == rDate &&
                    ((r.StartTime < rEnd && r.EndTime > rStart))); // Overlap logic

                result.Add(new { 
                    table.Id, 
                    table.TableNumber, 
                    table.FloorId,
                    IsAvailable = !isOccupied
                });
            }

            return Ok(result);
        }

        [HttpPost("Create")]
        public async Task<IActionResult> CreateReservation([FromBody] ReservationRequest request)
        {
            var currentStudentNumber = GetCurrentStudentNumber();

            if (IsAdmin)
            {
                if (string.IsNullOrWhiteSpace(request.StudentNumber))
                {
                    return BadRequest("Öğrenci numarası zorunludur.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(currentStudentNumber))
                {
                    return Forbid();
                }

                if (!string.IsNullOrWhiteSpace(request.StudentNumber) &&
                    !string.Equals(request.StudentNumber.Trim(), currentStudentNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }

                request.StudentNumber = currentStudentNumber;
            }

            if (request.StudentNumber.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Yönetici rezervasyon alamaz.");
            }

            if (!DateOnly.TryParse(request.ReservationDate, out var rDate) ||
                !TimeOnly.TryParse(request.StartTime, out var rStart) ||
                !TimeOnly.TryParse(request.EndTime, out var rEnd))
            {
                return BadRequest("Geçersiz tarih/saat formatı.");
            }

            var profile = await GetOrCreateStudentProfileAsync(request.StudentNumber, request.StudentType);
            await ApplyNoShowPenaltiesAsync(profile);

            // ===== PUAN BAZLI ERİŞİM KONTROLÜ =====
            // Admin kullanıcıları kontrol dışında tut
            if (!IsAdmin || !request.StudentNumber.Trim().Equals("admin", StringComparison.OrdinalIgnoreCase))
            {
                var accessCheck = await _priorityService.CheckAccessAsync(request.StudentNumber);
                if (!accessCheck.CanAccess)
                {
                    _logger.LogWarning(
                        "Access denied for {StudentNumber}. Score: {Score}, AllowedTime: {AllowedTime}, CurrentTime: {CurrentTime}",
                        request.StudentNumber, accessCheck.UserScore, accessCheck.AllowedTime, accessCheck.CurrentTime
                    );
                    return BadRequest(new
                    {
                        message = $"Rezervasyon sistemi sizin için henüz açılmadı. Puanınız: {accessCheck.UserScore}, Erişim saatiniz: {accessCheck.AllowedTime:hh\\:mm}",
                        userScore = accessCheck.UserScore,
                        allowedTime = accessCheck.AllowedTime.ToString(@"hh\:mm"),
                        remainingMinutes = accessCheck.RemainingMinutes
                    });
                }
            }
            // ===== PUAN BAZLI ERİŞİM KONTROLÜ SONU =====

            var today = DateOnly.FromDateTime(DateTime.Now);

            if (rEnd <= rStart)
            {
                return BadRequest(new
                {
                    message = "Bitiş saati başlangıç saatinden sonra olmalıdır."
                });
            }

            var reservationDuration = rEnd.ToTimeSpan() - rStart.ToTimeSpan();

            if (reservationDuration < MinReservationDuration)
            {
                return BadRequest(new
                {
                    message = $"Rezervasyon süresi en az {MinReservationDuration.TotalHours:F0} saat olmalıdır."
                });
            }

            if (reservationDuration > MaxReservationDuration)
            {
                return BadRequest(new
                {
                    message = $"Rezervasyon süresi en fazla {MaxReservationDuration.TotalHours:F0} saat olabilir."
                });
            }

            if (profile.BanUntil.HasValue && rDate <= profile.BanUntil.Value)
            {
                return BadRequest(new
                {
                    message = $"Ceza süreniz {profile.BanUntil:yyyy-MM-dd} tarihine kadar devam ediyor. Bu tarihten önceki bir gün için rezervasyon oluşturamazsınız.",
                    reason = profile.BanReason
                });
            }

            var rule = ResolveRule(profile.StudentType);

            if (rDate > today.AddDays(rule.MaxAdvanceDays))
            {
                return BadRequest(new
                {
                    message = $"En fazla {rule.MaxAdvanceDays} gün sonrası için rezervasyon oluşturabilirsiniz (bugün ve yarın)."
                });
            }

            // Yarın için rezervasyon açılma saati kontrolü
            if (rDate > today)
            {
                var accessTime = await _priorityService.GetAccessTimeAsync(request.StudentNumber);
                var now = DateTime.Now.TimeOfDay;
                
                if (now < accessTime)
                {
                    var accessTimeStr = $"{accessTime.Hours:D2}:{accessTime.Minutes:D2}";
                    return BadRequest(new
                    {
                        message = $"Yarın için rezervasyonlar sizin için saat {accessTimeStr}'de açılacak. Şu an sadece bugün için rezervasyon yapabilirsiniz."
                    });
                }
            }

            var activeReservationCount = await _context.Reservations
                .CountAsync(r => r.StudentNumber == request.StudentNumber && r.ReservationDate >= today);

            if (activeReservationCount >= rule.MaxActiveReservations)
            {
                return BadRequest(new
                {
                    message = $"Aynı anda en fazla {rule.MaxActiveReservations} aktif rezervasyonunuz olabilir. Lütfen mevcut rezervasyonlarınızı iptal edin veya kullanın."
                });
            }

            // Aynı öğrencinin diğer masalarda çakışan bir rezervasyonu olup olmadığını kontrol et
            var hasConflictForStudent = await _context.Reservations.AnyAsync(r =>
                r.StudentNumber == request.StudentNumber &&
                r.ReservationDate == rDate &&
                ((r.StartTime <= rStart && r.EndTime > rStart) ||
                 (r.StartTime < rEnd && r.EndTime >= rEnd)));

            if (hasConflictForStudent)
            {
                return BadRequest(new
                {
                    message = "Bu saat aralığında başka bir rezervasyonunuz bulunuyor."
                });
            }

            var isOccupied = await _context.Reservations.AnyAsync(r =>
                r.TableId == request.TableId &&
                r.ReservationDate == rDate &&
                ((r.StartTime <= rStart && r.EndTime > rStart) ||
                 (r.StartTime < rEnd && r.EndTime >= rEnd)));

            if (isOccupied)
            {
                return BadRequest(new
                {
                    message = "Bu saat aralığında masa dolu."
                });
            }

            // Score hesapla
            int score = await CalculateScoreAsync(profile, rDate);

            var reservation = new Reservation
            {
                TableId = request.TableId,
                StudentNumber = request.StudentNumber.Trim(),
                ReservationDate = rDate,
                StartTime = rStart,
                EndTime = rEnd,
                IsAttended = false,
                PenaltyProcessed = false,
                StudentType = profile.StudentType,
                Score = score,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reservations.Add(reservation);
            await _context.SaveChangesAsync();

            // RabbitMQ Event: Rezervasyon oluşturuldu
            try
            {
                var createdEvent = new ReservationCreatedEvent
                {
                    ReservationId = reservation.Id,
                    StudentNumber = reservation.StudentNumber,
                    TableId = reservation.TableId,
                    ReservationDate = reservation.ReservationDate.ToString("yyyy-MM-dd"),
                    StartTime = reservation.StartTime.ToString("HH:mm"),
                    EndTime = reservation.EndTime.ToString("HH:mm"),
                    StudentType = reservation.StudentType,
                    CreatedAt = DateTime.UtcNow
                };
                _publisher.Publish(createdEvent, "reservation.created");
                _logger.LogInformation("Reservation {ReservationId} created for student {StudentNumber}", reservation.Id, reservation.StudentNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish ReservationCreated event for reservation {ReservationId}", reservation.Id);
            }

            return Ok(new { message = "Rezervasyon oluşturuldu.", reservationId = reservation.Id });
        }

        [HttpGet("MyReservations")]
        public async Task<IActionResult> GetMyReservations(string studentNumber)
        {
            Console.WriteLine($"GetMyReservations called for: {studentNumber}");

            var currentStudentNumber = GetCurrentStudentNumber();
            if (!IsAdmin)
            {
                if (string.IsNullOrWhiteSpace(currentStudentNumber) ||
                    !string.Equals(studentNumber, currentStudentNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }

                studentNumber = currentStudentNumber;
            }

            var reservations = await _context.Reservations
                .Where(r => r.StudentNumber == studentNumber)
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.StartTime)
                .ToListAsync();

            Console.WriteLine($"Found {reservations.Count} reservations.");

            var result = new List<object>();
            foreach(var res in reservations)
            {
                var table = await _context.Tables.FindAsync(res.TableId);
                result.Add(new {
                    res.Id,
                    ReservationDate = res.ReservationDate.ToString("yyyy-MM-dd"),
                    StartTime = res.StartTime.ToString("HH:mm"),
                    EndTime = res.EndTime.ToString("HH:mm"),
                    res.IsAttended,
                    res.StudentType,
                    res.Score,
                    TableNumber = table?.TableNumber ?? "Bilinmiyor",
                    FloorId = table?.FloorId
                });
            }

            return Ok(result);
        }

        [HttpGet("All")]
        public async Task<IActionResult> GetAllReservations()
        {
            var reservations = await _context.Reservations
                .OrderByDescending(r => r.ReservationDate)
                .ThenByDescending(r => r.StartTime)
                .ToListAsync();

            var result = new List<object>();
            foreach(var res in reservations)
            {
                var table = await _context.Tables.FindAsync(res.TableId);
                result.Add(new {
                    res.Id,
                    res.StudentNumber,
                    ReservationDate = res.ReservationDate.ToString("yyyy-MM-dd"),
                    StartTime = res.StartTime.ToString("HH:mm"),
                    EndTime = res.EndTime.ToString("HH:mm"),
                    res.IsAttended,
                    res.StudentType,
                    TableNumber = table?.TableNumber ?? "Bilinmiyor",
                    FloorId = table?.FloorId
                });
            }

            return Ok(result);
        }

        [HttpDelete("Cancel/{id}")]
        public async Task<IActionResult> CancelReservation(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound("Rezervasyon bulunamadı.");
            }

            var currentStudentNumber = GetCurrentStudentNumber();
            if (!IsAdmin && (string.IsNullOrWhiteSpace(currentStudentNumber) ||
                !string.Equals(reservation.StudentNumber, currentStudentNumber, StringComparison.OrdinalIgnoreCase)))
            {
                return Forbid();
            }

            // Geçmiş rezervasyonlar iptal edilemez kuralı eklenebilir
            // if (reservation.ReservationDate < DateOnly.FromDateTime(DateTime.Now)) ...

            _context.Reservations.Remove(reservation);
            await _context.SaveChangesAsync();

            // RabbitMQ Event: Rezervasyon iptal edildi
            try
            {
                var cancelledEvent = new ReservationCancelledEvent
                {
                    ReservationId = reservation.Id,
                    StudentNumber = reservation.StudentNumber,
                    TableId = reservation.TableId,
                    ReservationDate = reservation.ReservationDate.ToString("yyyy-MM-dd"),
                    CancelledAt = DateTime.UtcNow
                };
                _publisher.Publish(cancelledEvent, "reservation.cancelled");
                _logger.LogInformation("Reservation {ReservationId} cancelled for student {StudentNumber}", reservation.Id, reservation.StudentNumber);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to publish ReservationCancelled event for reservation {ReservationId}", reservation.Id);
            }

            return Ok(new { message = "Rezervasyon iptal edildi." });
        }

        private static string BuildRuleKey(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return "lisans";
            }

            var cleaned = value.Trim().Replace(" ", string.Empty);
            cleaned = cleaned
                .Replace("İ", "i")
                .Replace("I", "i")
                .Replace("ı", "i")
                .Replace("Ü", "u")
                .Replace("ü", "u")
                .Replace("Ö", "o")
                .Replace("ö", "o")
                .Replace("Ğ", "g")
                .Replace("ğ", "g")
                .Replace("Ş", "s")
                .Replace("ş", "s")
                .Replace("Ç", "c")
                .Replace("ç", "c");

            return cleaned.ToLowerInvariant();
        }

        private static string NormalizeStudentType(string? rawType)
        {
            var ruleKey = BuildRuleKey(rawType);
            if (StudentTypeCanonicalNames.TryGetValue(ruleKey, out var canonical))
            {
                return canonical;
            }

            return StudentTypeCanonicalNames["lisans"];
        }

        private static StudentTypeRule ResolveRule(string studentType)
        {
            var key = BuildRuleKey(studentType);
            if (StudentTypeRules.TryGetValue(key, out var rule))
            {
                return rule;
            }

            return StudentTypeRules["lisans"];
        }

        private async Task<StudentProfile> GetOrCreateStudentProfileAsync(string studentNumber, string? studentTypeHint)
        {
            var normalizedNumber = studentNumber.Trim();
            var profile = await _context.StudentProfiles
                .SingleOrDefaultAsync(p => p.StudentNumber == normalizedNumber);

            var canonicalHint = string.IsNullOrWhiteSpace(studentTypeHint)
                ? null
                : NormalizeStudentType(studentTypeHint);

            if (profile == null)
            {
                profile = new StudentProfile
                {
                    StudentNumber = normalizedNumber,
                    StudentType = canonicalHint ?? "Lisans"
                };

                _context.StudentProfiles.Add(profile);
            }

            var identityApplied = await TryEnrichStudentTypeFromIdentityAsync(profile, normalizedNumber);

            if (!identityApplied && canonicalHint != null && !StudentTypeComparer.Equals(profile.StudentType, canonicalHint))
            {
                if (StudentTypeComparer.Equals(profile.StudentType, "Lisans"))
                {
                    profile.StudentType = canonicalHint;
                }
            }

            if (string.IsNullOrWhiteSpace(profile.StudentType))
            {
                profile.StudentType = "Lisans";
            }

            return profile;
        }

        private async Task<bool> TryEnrichStudentTypeFromIdentityAsync(StudentProfile profile, string normalizedStudentNumber)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("IdentityService");
                using var response = await client.GetAsync($"api/Auth/profile/{normalizedStudentNumber}");

                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("IdentityService returned {StatusCode} for student {StudentNumber}", response.StatusCode, normalizedStudentNumber);
                    return false;
                }

                var identityProfile = await response.Content.ReadFromJsonAsync<IdentityProfileDto>();
                if (identityProfile == null || string.IsNullOrWhiteSpace(identityProfile.AcademicLevel))
                {
                    return false;
                }

                var canonical = NormalizeStudentType(identityProfile.AcademicLevel);
                if (!StudentTypeComparer.Equals(profile.StudentType, canonical))
                {
                    profile.StudentType = canonical;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "IdentityService lookup failed for student {StudentNumber}", normalizedStudentNumber);
                return false;
            }
        }

        private async Task ApplyNoShowPenaltiesAsync(StudentProfile profile)
        {
            var nowLocal = DateTime.Now;
            var nowUtc = DateTime.UtcNow;
            var today = DateOnly.FromDateTime(nowLocal);
            var stateChanged = false;
            var penaltyAppliedThisCycle = false;

            if (profile.BanUntil.HasValue && profile.BanUntil.Value < today)
            {
                profile.BanUntil = null;
                profile.BanReason = null;
                stateChanged = true;
            }

            var overdueReservations = await _context.Reservations
                .Where(r => r.StudentNumber == profile.StudentNumber && !r.IsAttended && !r.PenaltyProcessed)
                .ToListAsync();

            foreach (var reservation in overdueReservations)
            {
                // Başlangıç saatinden 15dk sonrasına kadar giriş yapılmalı, aksi halde ceza
                var reservationStart = reservation.ReservationDate.ToDateTime(reservation.StartTime);
                if (reservationStart.AddMinutes(EntryGracePeriodMinutes) < nowLocal)
                {
                    // Direkt 2 günlük ban uygula
                    profile.BanUntil = DateOnly.FromDateTime(nowLocal.AddDays(2));
                    profile.BanReason = "Rezervasyonunuza katılmadığınız için sistem 2 gün ceza uyguladı.";
                    reservation.PenaltyProcessed = true;
                    stateChanged = true;
                    penaltyAppliedThisCycle = true;
                }
            }

            if (stateChanged)
            {
                profile.LastNoShowProcessedAt = nowUtc;
                await _context.SaveChangesAsync();
            }
        }

        [HttpGet("Profile/{studentNumber}")]
        public async Task<IActionResult> GetProfile(string studentNumber)
        {
            if (string.IsNullOrWhiteSpace(studentNumber))
            {
                return BadRequest(new { message = "Öğrenci numarası zorunludur." });
            }

            if (!IsAdmin)
            {
                var currentStudentNumber = GetCurrentStudentNumber();
                if (string.IsNullOrWhiteSpace(currentStudentNumber) ||
                    !string.Equals(studentNumber, currentStudentNumber, StringComparison.OrdinalIgnoreCase))
                {
                    return Forbid();
                }
            }

            var profile = await GetOrCreateStudentProfileAsync(studentNumber, null);
            await ApplyNoShowPenaltiesAsync(profile);

            await _context.SaveChangesAsync();

            var response = new StudentProfileDto
            {
                StudentNumber = profile.StudentNumber,
                StudentType = profile.StudentType,
                PenaltyPoints = 0,
                BanUntil = profile.BanUntil?.ToString("yyyy-MM-dd"),
                BanReason = profile.BanReason,
                LastNoShowProcessedAt = profile.LastNoShowProcessedAt
            };

            return Ok(response);
        }

        [HttpGet("Penalties")]
        public async Task<IActionResult> GetPenalties()
        {
            var profiles = await _context.StudentProfiles
                .Where(p => p.BanUntil != null)
                .OrderByDescending(p => p.BanUntil)
                .ToListAsync();

            var result = profiles.Select(p => new StudentProfileDto
            {
                StudentNumber = p.StudentNumber,
                StudentType = p.StudentType,
                PenaltyPoints = 0,
                BanUntil = p.BanUntil?.ToString("yyyy-MM-dd"),
                BanReason = p.BanReason,
                LastNoShowProcessedAt = p.LastNoShowProcessedAt
            });

            return Ok(result);
        }

        [HttpGet("CheckAccess")]
        public async Task<IActionResult> CheckAccess(string studentNumber)
        {
            if (string.IsNullOrWhiteSpace(studentNumber))
            {
                return Ok(new { allowed = false, message = "Geçerli bir öğrenci numarası gönderilmedi." });
            }

            var normalizedStudentNumber = studentNumber.Trim();
            var nowLocal = DateTime.Now;
            var today = DateOnly.FromDateTime(nowLocal);
            var nowTime = TimeOnly.FromDateTime(nowLocal);

            var profile = await GetOrCreateStudentProfileAsync(normalizedStudentNumber, null);
            await ApplyNoShowPenaltiesAsync(profile);

            if (profile.BanUntil.HasValue && profile.BanUntil.Value >= today)
            {
                return Ok(new
                {
                    allowed = false,
                    message = $"Ceza sisteminden dolayı {profile.BanUntil:yyyy-MM-dd} tarihine kadar giriş hakkınız bulunmuyor.",
                    reason = profile.BanReason
                });
            }

            var todaysReservations = await _context.Reservations
                .Where(r => r.ReservationDate == today && EF.Functions.ILike(r.StudentNumber, normalizedStudentNumber))
                .OrderBy(r => r.StartTime)
                .ToListAsync();

            // Giriş yapılabilir aralık: Başlangıç - 5dk ile Başlangıç + 15dk (veya EndTime) arasında
            var activeReservation = todaysReservations
                .FirstOrDefault(r =>
                    r.StartTime.AddMinutes(-EarlyToleranceMinutes) <= nowTime &&
                    r.EndTime >= nowTime);

            if (activeReservation != null)
            {
                // Başlangıç + 15dk geçtiyse artık giriş yapılamaz (ceza alır)
                var entryDeadline = activeReservation.StartTime.AddMinutes(EntryGracePeriodMinutes);
                if (nowTime > entryDeadline && !activeReservation.IsAttended)
                {
                    return Ok(new
                    {
                        allowed = false,
                        message = $"Giriş süresi doldu! Rezervasyon başlangıcından itibaren {EntryGracePeriodMinutes} dakika içinde giriş yapılmalıydı. Ceza puanı uygulanacak."
                    });
                }

                if (!activeReservation.IsAttended)
                {
                    activeReservation.IsAttended = true;
                    await _context.SaveChangesAsync();
                }

                return Ok(new
                {
                    allowed = true,
                    message = $"Giriş onaylandı. Masa: {activeReservation.TableId}",
                    reservationId = activeReservation.Id
                });
            }

            var upcomingToday = todaysReservations
                .FirstOrDefault(r => nowTime < r.StartTime.AddMinutes(-EarlyToleranceMinutes));

            if (upcomingToday != null)
            {
                var waitMinutes = Math.Ceiling((upcomingToday.StartTime.AddMinutes(-EarlyToleranceMinutes) - nowTime).TotalMinutes);
                return Ok(new
                {
                    allowed = false,
                    message = $"Rezervasyon saati henüz başlamadı. Tahmini bekleme: {waitMinutes} dakika. ({upcomingToday.StartTime} - {upcomingToday.EndTime})"
                });
            }

            if (todaysReservations.Count > 0)
            {
                var lastReservation = todaysReservations.Last();
                return Ok(new
                {
                    allowed = false,
                    message = $"Rezervasyon saati sona erdi. ({lastReservation.StartTime} - {lastReservation.EndTime})"
                });
            }

            var nextReservation = await _context.Reservations
                .Where(r => r.ReservationDate > today && EF.Functions.ILike(r.StudentNumber, normalizedStudentNumber))
                .OrderBy(r => r.ReservationDate)
                .ThenBy(r => r.StartTime)
                .FirstOrDefaultAsync();

            if (nextReservation != null)
            {
                return Ok(new
                {
                    allowed = false,
                    message = $"Sonraki rezervasyonunuz {nextReservation.ReservationDate:yyyy-MM-dd} tarihinde. ({nextReservation.StartTime} - {nextReservation.EndTime})"
                });
            }

            return Ok(new { allowed = false, message = "Şu an için aktif bir rezervasyonunuz bulunmamaktadır." });
        }

        private async Task<int> CalculateScoreAsync(StudentProfile profile, DateOnly reservationDate)
        {
            // Doktora: 300 puan (sınav bonusu yok)
            if (StudentTypeComparer.Equals(profile.StudentType, "Doktora"))
            {
                return ScoreDoktora;
            }

            // Yüksek Lisans: 200 puan (sınav bonusu yok)
            if (StudentTypeComparer.Equals(profile.StudentType, "YüksekLisans"))
            {
                return ScoreYuksekLisans;
            }

            // Lisans: Base 100 puan
            int score = ScoreLisans;

            // Sınav haftasındaysa +50 bonus (sadece Lisans için)
            if (profile.FacultyId.HasValue)
            {
                var examSchedule = await _context.ExamSchedules
                    .FirstOrDefaultAsync(e => e.FacultyId == profile.FacultyId.Value);

                if (examSchedule != null)
                {
                    if (reservationDate >= examSchedule.ExamWeekStart && reservationDate <= examSchedule.ExamWeekEnd)
                    {
                        score += ScoreLisansExamBonus;
                    }
                }
            }

            return score;
        }

        /// <summary>
        /// Öğrencinin rezervasyon sistemine erişim durumunu kontrol eder
        /// </summary>
        [HttpPost("CheckAccess")]
        public async Task<IActionResult> CheckAccess([FromBody] AccessCheckRequest request)
        {
            if (string.IsNullOrEmpty(request.StudentNumber))
            {
                return BadRequest(new { message = "Öğrenci numarası gerekli." });
            }

            var accessResult = await _priorityService.CheckAccessAsync(request.StudentNumber);

            return Ok(new
            {
                canAccess = accessResult.CanAccess,
                userScore = accessResult.UserScore,
                allowedTime = accessResult.AllowedTime.ToString(@"hh\:mm"),
                currentTime = accessResult.CurrentTime.ToString(@"hh\:mm\:ss"),
                remainingMinutes = accessResult.RemainingMinutes,
                message = accessResult.CanAccess 
                    ? "Rezervasyon yapabilirsiniz." 
                    : $"Rezervasyon sistemi {accessResult.AllowedTime:hh\\:mm} saatinde açılacak. Puanınız: {accessResult.UserScore}"
            });
        }

        [HttpPost("SetExamWeek")]
        public async Task<IActionResult> SetExamWeek([FromBody] SetExamWeekRequest request)
        {
            if (!IsAdmin)
            {
                return Forbid();
            }

            if (request.FacultyId <= 0)
            {
                return BadRequest(new { message = "Geçerli bir Fakülte seçilmelidir." });
            }

            if (!DateOnly.TryParse(request.ExamWeekStart, out var startDate) ||
                !DateOnly.TryParse(request.ExamWeekEnd, out var endDate))
            {
                return BadRequest(new { message = "Geçersiz tarih formatı." });
            }

            if (endDate < startDate)
            {
                return BadRequest(new { message = "Bitiş tarihi başlangıç tarihinden önce olamaz." });
            }

            // Fakülte var mı kontrol et
            var faculty = await _context.Faculties.FindAsync(request.FacultyId);
            if (faculty == null)
            {
                return NotFound(new { message = "Fakülte bulunamadı." });
            }

            // ExamSchedule tablosunda kontrol et - varsa güncelle, yoksa oluştur
            var examSchedule = await _context.ExamSchedules
                .FirstOrDefaultAsync(e => e.FacultyId == request.FacultyId);

            if (examSchedule == null)
            {
                // Yeni fakülte sınav haftası oluştur
                examSchedule = new ExamSchedule
                {
                    FacultyId = request.FacultyId,
                    ExamWeekStart = startDate,
                    ExamWeekEnd = endDate,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ExamSchedules.Add(examSchedule);
            }
            else
            {
                // Mevcut sınav haftasını güncelle
                examSchedule.ExamWeekStart = startDate;
                examSchedule.ExamWeekEnd = endDate;
                examSchedule.UpdatedAt = DateTime.UtcNow;
            }

            await _context.SaveChangesAsync();

            // Bu fakültede kaç öğrenci var?
            var studentCount = await _context.StudentProfiles
                .Where(p => p.FacultyId == request.FacultyId && p.StudentType == "Lisans")
                .CountAsync();

            _logger.LogInformation("Exam week set for faculty {FacultyId} ({FacultyName}): {Start} to {End}, affects {Count} students", 
                request.FacultyId, faculty.Name, startDate, endDate, studentCount);

            return Ok(new { 
                message = $"{faculty.Name} fakültesi için sınav haftası ayarlandı.",
                affectedStudents = studentCount
            });
        }

        [HttpGet("ExamWeeks")]
        public async Task<IActionResult> GetExamWeeks()
        {
            if (!IsAdmin)
            {
                return Forbid();
            }

            var examWeeks = await _context.ExamSchedules
                .Include(e => e.Faculty)
                .Select(e => new
                {
                    e.Id,
                    FacultyId = e.FacultyId,
                    FacultyName = e.Faculty.Name,
                    ExamWeekStart = e.ExamWeekStart.ToString("yyyy-MM-dd"),
                    ExamWeekEnd = e.ExamWeekEnd.ToString("yyyy-MM-dd"),
                    StudentCount = _context.StudentProfiles.Count(p => p.FacultyId == e.FacultyId && p.StudentType == "Lisans")
                })
                .OrderBy(x => x.FacultyName)
                .ToListAsync();

            return Ok(examWeeks);
        }

        [HttpGet("Faculties")]
        public async Task<IActionResult> GetFaculties()
        {
            var faculties = await _context.Faculties
                .OrderBy(f => f.Name)
                .Select(f => new { f.Id, f.Name })
                .ToListAsync();

            return Ok(faculties);
        }

        [HttpPost("UpdateStudentDepartment")]
        public async Task<IActionResult> UpdateStudentDepartment([FromBody] UpdateDepartmentRequest request)
        {
            _logger.LogInformation("UpdateStudentDepartment called: StudentNumber={StudentNumber}, FacultyId={FacultyId}, Department={Department}, StudentType={StudentType}",
                request?.StudentNumber ?? "NULL", request?.FacultyId ?? 0, request?.Department ?? "NULL", request?.StudentType ?? "NULL");

            if (request == null)
            {
                _logger.LogWarning("UpdateStudentDepartment: Request is null");
                return BadRequest(new { message = "Geçersiz istek." });
            }

            if (string.IsNullOrWhiteSpace(request.StudentNumber) || 
                request.FacultyId <= 0 || 
                string.IsNullOrWhiteSpace(request.Department))
            {
                _logger.LogWarning("UpdateStudentDepartment validation failed: StudentNumber={StudentNumber}, FacultyId={FacultyId}, Department={Department}",
                    request.StudentNumber, request.FacultyId, request.Department);
                return BadRequest(new { message = "Öğrenci numarası, fakülte ve bölüm adı zorunludur." });
            }

            var profile = await _context.StudentProfiles
                .FirstOrDefaultAsync(p => p.StudentNumber == request.StudentNumber);

            if (profile == null)
            {
                // Profil yoksa oluştur
                profile = new StudentProfile
                {
                    StudentNumber = request.StudentNumber,
                    StudentType = !string.IsNullOrWhiteSpace(request.StudentType) ? request.StudentType : "Lisans",
                    FacultyId = request.FacultyId,
                    Department = request.Department
                };
                _context.StudentProfiles.Add(profile);
                _logger.LogInformation("StudentProfile created for {StudentNumber}: Type={Type}, FacultyId={FacultyId}, Department={Department}",
                    request.StudentNumber, profile.StudentType, request.FacultyId, request.Department);
            }
            else
            {
                // Profil varsa güncelle
                profile.FacultyId = request.FacultyId;
                profile.Department = request.Department;
                if (!string.IsNullOrWhiteSpace(request.StudentType))
                {
                    profile.StudentType = request.StudentType;
                }
                _logger.LogInformation("StudentProfile updated for {StudentNumber}", request.StudentNumber);
            }
            
            await _context.SaveChangesAsync();

            return Ok(new { message = "Öğrenci bilgileri güncellendi." });
        }
    }

    public class ReservationRequest
    {
        public int TableId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string ReservationDate { get; set; } = string.Empty;
        public string StartTime { get; set; } = string.Empty;
        public string EndTime { get; set; } = string.Empty;
        public string? StudentType { get; set; }
    }

    public class StudentProfileDto
    {
        public string StudentNumber { get; set; } = string.Empty;
        public string StudentType { get; set; } = string.Empty;
        public int PenaltyPoints { get; set; }
        public string? BanUntil { get; set; }
        public string? BanReason { get; set; }
        public DateTime? LastNoShowProcessedAt { get; set; }
    }

    public class SetExamWeekRequest
    {
        public int FacultyId { get; set; }
        public string ExamWeekStart { get; set; } = string.Empty;
        public string ExamWeekEnd { get; set; } = string.Empty;
    }

    public class UpdateDepartmentRequest
    {
        public string StudentNumber { get; set; } = string.Empty;
        public int FacultyId { get; set; }
        public string Department { get; set; } = string.Empty;
        public string? StudentType { get; set; }
    }

    public class AccessCheckRequest
    {
        public string StudentNumber { get; set; } = string.Empty;
    }
}