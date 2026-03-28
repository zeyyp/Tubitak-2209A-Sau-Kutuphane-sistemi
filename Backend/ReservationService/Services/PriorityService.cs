using Microsoft.EntityFrameworkCore;
using ReservationService.Data;

namespace ReservationService.Services
{
    public class PriorityService
    {
        private readonly ReservationDbContext _context;
        private readonly ILogger<PriorityService> _logger;

        // Puan sabitleri
        private const int ScoreDoktora = 300;
        private const int ScoreYuksekLisans = 200;
        private const int ScoreLisans = 100;
        private const int ExamWeekBonus = 50;

        // Erişim saatleri (Test için: 17:00, 17:05, 17:10, 17:15)
        private static readonly Dictionary<int, TimeSpan> AccessTimes = new()
        {
            { 300, new TimeSpan(17, 0, 0) },   // Doktora: 17:00
            { 200, new TimeSpan(17, 5, 0) },   // YL: 17:05
            { 150, new TimeSpan(17, 10, 0) },  // Sınavlı Lisans: 17:10
            { 100, new TimeSpan(17, 15, 0) }   // Normal Lisans: 17:15
        };

        public PriorityService(ReservationDbContext context, ILogger<PriorityService> logger)
        {
            _context = context;
            _logger = logger;
        }

        /// <summary>
        /// Öğrencinin toplam puanını hesaplar (Akademik Seviye + Sınav Bonusu)
        /// </summary>
        public async Task<int> CalculateUserScoreAsync(string studentNumber)
        {
            var student = await _context.StudentProfiles
                .Include(s => s.Faculty)
                .FirstOrDefaultAsync(s => s.StudentNumber == studentNumber);

            if (student == null)
            {
                _logger.LogWarning("Student not found: {StudentNumber}", studentNumber);
                return 0;
            }

            // Temel puan (Akademik seviye)
            int baseScore = student.StudentType.ToLower() switch
            {
                "doktora" => ScoreDoktora,
                "yukseklisans" or "yükseklisans" => ScoreYuksekLisans,
                _ => ScoreLisans
            };

            // Sınav haftası bonusu kontrolü
            int examBonus = await CheckExamWeekBonusAsync(student.FacultyId ?? 0);

            int totalScore = baseScore + examBonus;

            _logger.LogInformation(
                "Score calculated for {StudentNumber}: Base={Base}, ExamBonus={Bonus}, Total={Total}",
                studentNumber, baseScore, examBonus, totalScore
            );

            return totalScore;
        }

        /// <summary>
        /// Öğrencinin fakultesinde aktif sınav haftası varsa bonus döndürür
        /// </summary>
        private async Task<int> CheckExamWeekBonusAsync(int facultyId)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            var activeExamSchedule = await _context.ExamSchedules
                .Where(e => e.FacultyId == facultyId)
                .Where(e => e.ExamWeekStart <= today && today <= e.ExamWeekEnd)
                .FirstOrDefaultAsync();

            return activeExamSchedule != null ? ExamWeekBonus : 0;
        }

        /// <summary>
        /// Kullanıcının rezervasyon sistemine erişip erişemeyeceğini kontrol eder
        /// </summary>
        public async Task<AccessCheckResult> CheckAccessAsync(string studentNumber)
        {
            int userScore = await CalculateUserScoreAsync(studentNumber);
            var now = DateTime.Now.TimeOfDay;

            // Puanına göre erişim saatini bul
            var allowedTime = GetAccessTimeForScore(userScore);

            bool canAccess = now >= allowedTime;

            return new AccessCheckResult
            {
                CanAccess = canAccess,
                UserScore = userScore,
                AllowedTime = allowedTime,
                CurrentTime = now,
                RemainingMinutes = canAccess ? 0 : (int)(allowedTime - now).TotalMinutes
            };
        }

        /// <summary>
        /// Puana göre erişim saatini döndürür
        /// </summary>
        private TimeSpan GetAccessTimeForScore(int score)
        {
            // En yüksek puandan başlayarak uygun zaman dilimini bul
            foreach (var kvp in AccessTimes.OrderByDescending(x => x.Key))
            {
                if (score >= kvp.Key)
                {
                    return kvp.Value;
                }
            }

            // Eğer hiçbir kurala uymuyorsa en düşük seviyeyi ver
            return AccessTimes[100];
        }

        /// <summary>
        /// Öğrenci numarasına göre erişim saatini döndürür
        /// </summary>
        public async Task<TimeSpan> GetAccessTimeAsync(string studentNumber)
        {
            var score = await CalculateUserScoreAsync(studentNumber);
            return GetAccessTimeForScore(score);
        }

        /// <summary>
        /// Rezervasyon prioritesini hesaplar (RabbitMQ için)
        /// </summary>
        public byte CalculatePriority(int userScore)
        {
            // RabbitMQ priority: 0-10 arası (10 en yüksek)
            // 300+ -> 10
            // 200-299 -> 8
            // 150-199 -> 6
            // 100-149 -> 4
            // 0-99 -> 1

            if (userScore >= 300) return 10;
            if (userScore >= 200) return 8;
            if (userScore >= 150) return 6;
            if (userScore >= 100) return 4;
            return 1;
        }
    }

    public class AccessCheckResult
    {
        public bool CanAccess { get; set; }
        public int UserScore { get; set; }
        public TimeSpan AllowedTime { get; set; }
        public TimeSpan CurrentTime { get; set; }
        public int RemainingMinutes { get; set; }
    }
}
