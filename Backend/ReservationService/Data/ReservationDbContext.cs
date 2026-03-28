using Microsoft.EntityFrameworkCore;

namespace ReservationService.Data
{
    public class ReservationDbContext : DbContext
    {
        public ReservationDbContext(DbContextOptions<ReservationDbContext> options) : base(options) { }

        public DbSet<Reservation> Reservations { get; set; } = null!;
        public DbSet<Table> Tables { get; set; } = null!;
        public DbSet<StudentProfile> StudentProfiles { get; set; } = null!;
        public DbSet<ExamSchedule> ExamSchedules { get; set; } = null!;
        public DbSet<Faculty> Faculties { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<StudentProfile>()
                .HasIndex(p => p.StudentNumber)
                .IsUnique();

            modelBuilder.Entity<ExamSchedule>()
                .HasIndex(e => e.FacultyId)
                .IsUnique();

            modelBuilder.Entity<Faculty>()
                .HasIndex(f => f.Name)
                .IsUnique();

            // Faculty -> StudentProfile (1:Many)
            modelBuilder.Entity<StudentProfile>()
                .HasOne(s => s.Faculty)
                .WithMany(f => f.StudentProfiles)
                .HasForeignKey(s => s.FacultyId)
                .OnDelete(DeleteBehavior.SetNull);

            // Faculty -> ExamSchedule (1:Many)
            modelBuilder.Entity<ExamSchedule>()
                .HasOne(e => e.Faculty)
                .WithMany(f => f.ExamSchedules)
                .HasForeignKey(e => e.FacultyId)
                .OnDelete(DeleteBehavior.Cascade);

            // Reservation -> Overlap query index (Concurrency and Performance)
            modelBuilder.Entity<Reservation>()
                .HasIndex(r => new { r.TableId, r.ReservationDate, r.StartTime, r.EndTime });
        }
    }

    public class Reservation
    {
        public int Id { get; set; }
        public int TableId { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public DateOnly ReservationDate { get; set; }
        public TimeOnly StartTime { get; set; }
        public TimeOnly EndTime { get; set; }
        public bool IsAttended { get; set; }
        public bool PenaltyProcessed { get; set; }
        public string StudentType { get; set; } = "Lisans";
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int Score { get; set; }
    }

    public class Table
    {
        public int Id { get; set; }
        public string TableNumber { get; set; } = string.Empty;
        public int FloorId { get; set; }
    }

    public class StudentProfile
    {
        public int Id { get; set; }
        public string StudentNumber { get; set; } = string.Empty;
        public string StudentType { get; set; } = "Lisans";
        public int? FacultyId { get; set; }
        public Faculty? Faculty { get; set; }
        public string? Department { get; set; }
        public DateOnly? BanUntil { get; set; }
        public DateTime? LastNoShowProcessedAt { get; set; }
        public string? BanReason { get; set; }
    }

    public class ExamSchedule
    {
        public int Id { get; set; }
        public int FacultyId { get; set; }
        public Faculty Faculty { get; set; } = null!;
        public DateOnly ExamWeekStart { get; set; }
        public DateOnly ExamWeekEnd { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
    }

    public class Faculty
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public ICollection<StudentProfile> StudentProfiles { get; set; } = new List<StudentProfile>();
        public ICollection<ExamSchedule> ExamSchedules { get; set; } = new List<ExamSchedule>();
    }
}