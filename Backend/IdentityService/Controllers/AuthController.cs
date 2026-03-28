using IdentityService.Data;
using IdentityService.Entities;
using IdentityService.Options;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;

namespace IdentityService.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<AppUser> _userManager;
        private readonly SignInManager<AppUser> _signInManager;
        private readonly JwtOptions _jwtOptions;
        private readonly ILogger<AuthController> _logger;
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IConfiguration _configuration;
        private readonly AppDbContext _dbContext;

        public AuthController(
            UserManager<AppUser> userManager,
            SignInManager<AppUser> signInManager,
            IOptions<JwtOptions> jwtOptions,
            ILogger<AuthController> logger,
            IHttpClientFactory httpClientFactory,
            IConfiguration configuration,
            AppDbContext dbContext)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _jwtOptions = jwtOptions.Value;
            _logger = logger;
            _httpClientFactory = httpClientFactory;
            _configuration = configuration;
            _dbContext = dbContext;
        }

        [HttpPost("login")]
        [AllowAnonymous]
        public async Task<IActionResult> Login([FromBody] LoginModel model)
        {
            if (model == null || string.IsNullOrWhiteSpace(model.StudentNumber) || string.IsNullOrWhiteSpace(model.Password))
            {
                return BadRequest("Öğrenci numarası ve şifre zorunludur");
            }

            var normalizedStudentNumber = model.StudentNumber.Trim();
            var user = await _userManager.FindByNameAsync(normalizedStudentNumber);
            if (user == null)
            {
                return Unauthorized("Öğrenci numarası veya şifre hatalı");
            }

            // Hesap kilitli mi kontrol et
            if (await _userManager.IsLockedOutAsync(user))
            {
                var lockoutEnd = await _userManager.GetLockoutEndDateAsync(user);
                var remainingMinutes = lockoutEnd.HasValue 
                    ? Math.Ceiling((lockoutEnd.Value - DateTimeOffset.UtcNow).TotalMinutes) 
                    : 0;
                
                _logger.LogWarning("User {StudentNumber} attempted login while locked out", user.StudentNumber);
                return Unauthorized($"Hesap geçici olarak kilitlendi. {remainingMinutes} dakika sonra tekrar deneyin.");
            }

            var signInResult = await _signInManager.CheckPasswordSignInAsync(user, model.Password, lockoutOnFailure: true);
            if (!signInResult.Succeeded)
            {
                if (signInResult.IsLockedOut)
                {
                    _logger.LogWarning("User {StudentNumber} account locked after failed login attempts", user.StudentNumber);
                    return Unauthorized("Çok fazla başarısız deneme. Hesap 15 dakika süreyle kilitlendi.");
                }
                
                return Unauthorized("Öğrenci numarası veya şifre hatalı");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var tokenResult = GenerateToken(user, roles);
            
            // Refresh Token oluştur
            var refreshToken = await GenerateRefreshTokenAsync(user.Id, GetClientIpAddress());
            
            _logger.LogInformation("User {StudentNumber} logged in successfully with refresh token", user.StudentNumber);

            return Ok(new
            {
                message = "Giriş başarılı",
                accessToken = tokenResult.Token,
                refreshToken = refreshToken.Token,
                accessTokenExpiresAt = tokenResult.ExpiresAt,
                refreshTokenExpiresAt = refreshToken.ExpiresAt,
                studentNumber = user.StudentNumber,
                role = roles.FirstOrDefault() ?? "student",
                academicLevel = user.AcademicLevel,
                fullName = user.FullName
            });
        }
        
        /// <summary>
        /// Refresh Token ile yeni Access Token al
        /// </summary>
        [HttpPost("refresh")]
        [AllowAnonymous]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest("Refresh token gerekli");
            }

            // Refresh token'ı veritabanında bul
            var storedToken = await _dbContext.RefreshTokens
                .Include(rt => rt.User)
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

            if (storedToken == null)
            {
                _logger.LogWarning("Refresh token not found: {Token}", request.RefreshToken);
                return Unauthorized("Geçersiz refresh token");
            }

            // Token aktif mi kontrol et
            if (!storedToken.IsActive)
            {
                _logger.LogWarning("Refresh token is not active: Revoked={Revoked}, Used={Used}, Expired={Expired}", 
                    storedToken.IsRevoked, storedToken.IsUsed, DateTime.UtcNow >= storedToken.ExpiresAt);
                return Unauthorized("Refresh token kullanılmış, iptal edilmiş veya süresi dolmuş");
            }

            // Eski token'ı kullanıldı olarak işaretle
            storedToken.IsUsed = true;
            storedToken.UsedByIp = GetClientIpAddress();
            await _dbContext.SaveChangesAsync();

            // Yeni token çifti oluştur
            var user = storedToken.User;
            var roles = await _userManager.GetRolesAsync(user);
            var newAccessToken = GenerateToken(user, roles);
            var newRefreshToken = await GenerateRefreshTokenAsync(user.Id, GetClientIpAddress());

            _logger.LogInformation("Tokens refreshed for user {StudentNumber}", user.StudentNumber);

            return Ok(new
            {
                message = "Token yenilendi",
                accessToken = newAccessToken.Token,
                refreshToken = newRefreshToken.Token,
                accessTokenExpiresAt = newAccessToken.ExpiresAt,
                refreshTokenExpiresAt = newRefreshToken.ExpiresAt,
                studentNumber = user.StudentNumber,
                role = roles.FirstOrDefault() ?? "student"
            });
        }
        
        /// <summary>
        /// Refresh Token'ı iptal et (logout)
        /// </summary>
        [HttpPost("revoke")]
        [Authorize]
        public async Task<IActionResult> RevokeToken([FromBody] RefreshTokenRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest("Refresh token gerekli");
            }

            var storedToken = await _dbContext.RefreshTokens
                .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken);

            if (storedToken == null)
            {
                return NotFound("Token bulunamadı");
            }

            // Sadece kendi token'ını iptal edebilir
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (storedToken.UserId != userId)
            {
                return Forbid();
            }

            storedToken.IsRevoked = true;
            await _dbContext.SaveChangesAsync();

            _logger.LogInformation("Refresh token revoked for user {UserId}", userId);

            return Ok(new { message = "Token iptal edildi" });
        }

            [HttpPost("register")]
            [AllowAnonymous]
            public async Task<IActionResult> Register([FromBody] RegisterModel model)
            {
                if (model == null)
                {
                    return BadRequest("Geçersiz kayıt isteği");
                }

                var user = new AppUser
                {
                    UserName = model.StudentNumber?.Trim(),
                    Email = model.Email,
                    StudentNumber = model.StudentNumber?.Trim(),
                    FullName = model.FullName,
                    AcademicLevel = model.AcademicLevel,
                    EmailConfirmed = true
                };

                var result = await _userManager.CreateAsync(user, model.Password);

                if (!result.Succeeded)
                {
                    return BadRequest(result.Errors);
                }

                var roleResult = await _userManager.AddToRoleAsync(user, "student");
                if (!roleResult.Succeeded)
                {
                    _logger.LogWarning("Yeni kullanıcı {StudentNumber} role eklenemedi: {Errors}", user.StudentNumber, string.Join(",", roleResult.Errors.Select(e => e.Description)));
                }

                // StudentProfile oluştur (ReservationService'e kaydet)
                if (model.FacultyId.HasValue && model.FacultyId.Value > 0)
                {
                    try
                    {
                        var httpClient = _httpClientFactory.CreateClient();
                        var updateRequest = new
                        {
                            studentNumber = user.StudentNumber,
                            facultyId = model.FacultyId.Value,
                            department = model.Department ?? "",
                            studentType = model.AcademicLevel ?? "Lisans"
                        };
                        
                        _logger.LogInformation("Creating StudentProfile for {StudentNumber} with FacultyId={FacultyId}, Department={Department}, StudentType={StudentType}", 
                            user.StudentNumber, model.FacultyId.Value, model.Department, model.AcademicLevel);
                        
                        var reservationServiceUrl = _configuration["Services:ReservationServiceUrl"] ?? "http://localhost:5002";
                        var response = await httpClient.PostAsJsonAsync(
                            $"{reservationServiceUrl}/api/Reservation/UpdateStudentDepartment",
                            updateRequest);
                        
                        if (response.IsSuccessStatusCode)
                        {
                            _logger.LogInformation("StudentProfile created for {StudentNumber}", user.StudentNumber);
                        }
                        else
                        {
                            var errorContent = await response.Content.ReadAsStringAsync();
                            _logger.LogWarning("Failed to create StudentProfile for {StudentNumber}: {Status} - {Error}", 
                                user.StudentNumber, response.StatusCode, errorContent);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error creating StudentProfile for {StudentNumber}", user.StudentNumber);
                    }
                }

                return Ok(new { message = "Kayıt başarılı" });
            }

            [HttpGet("profile/{studentNumber}")]
            [Authorize]
            public async Task<IActionResult> GetProfile(string studentNumber)
            {
                if (string.IsNullOrWhiteSpace(studentNumber))
                {
                    return BadRequest("Öğrenci numarası zorunludur");
                }

                var user = await _userManager.FindByNameAsync(studentNumber.Trim());
                if (user == null)
                {
                    return NotFound("Kullanıcı bulunamadı");
                }

                var roles = await _userManager.GetRolesAsync(user);

                return Ok(new
                {
                    studentNumber = user.StudentNumber,
                    fullName = user.FullName,
                    academicLevel = user.AcademicLevel,
                    role = roles.FirstOrDefault() ?? "student",
                    email = user.Email
                });
            }

        private (string Token, DateTime ExpiresAt) GenerateToken(AppUser user, IList<string> roles)
        {
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id),
                new Claim("studentNumber", user.StudentNumber ?? string.Empty),
                new Claim("academicLevel", user.AcademicLevel ?? string.Empty),
                new Claim("fullName", user.FullName ?? string.Empty)
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.Key));
            var creds = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
            var expiresAt = DateTime.UtcNow.AddMinutes(_jwtOptions.AccessTokenMinutes);

            var token = new JwtSecurityToken(
                issuer: _jwtOptions.Issuer,
                audience: _jwtOptions.Audience,
                claims: claims,
                expires: expiresAt,
                signingCredentials: creds);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expiresAt);
        }
        
        /// <summary>
        /// Güvenli rastgele Refresh Token oluşturur ve veritabanına kaydeder
        /// </summary>
        private async Task<RefreshToken> GenerateRefreshTokenAsync(string userId, string? ipAddress)
        {
            // Güvenli rastgele token oluştur (256 bit)
            var randomBytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(randomBytes);
            var tokenString = Convert.ToBase64String(randomBytes);
            
            var refreshToken = new RefreshToken
            {
                Token = tokenString,
                UserId = userId,
                CreatedAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddDays(_jwtOptions.RefreshTokenDays),
                CreatedByIp = ipAddress
            };
            
            _dbContext.RefreshTokens.Add(refreshToken);
            await _dbContext.SaveChangesAsync();
            
            return refreshToken;
        }
        
        /// <summary>
        /// Client IP adresini alır (proxy arkasında çalışıyorsa X-Forwarded-For header'ını kontrol eder)
        /// </summary>
        private string? GetClientIpAddress()
        {
            // X-Forwarded-For header'ını kontrol et (proxy/load balancer arkasındaysa)
            if (Request.Headers.ContainsKey("X-Forwarded-For"))
            {
                return Request.Headers["X-Forwarded-For"].ToString().Split(',').FirstOrDefault()?.Trim();
            }
            
            // RemoteIpAddress'i al
            return HttpContext.Connection.RemoteIpAddress?.ToString();
        }
    }

    public class LoginModel
    {
        public string StudentNumber { get; set; }
        public string Password { get; set; }
    }

    public class RegisterModel
    {
        public string StudentNumber { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public string Password { get; set; }
        public string AcademicLevel { get; set; }
        public int? FacultyId { get; set; }
        public string? Department { get; set; }
    }
    
    public class RefreshTokenRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
    }
}