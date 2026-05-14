using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using Shared.Events;

namespace ReservationService.Services;

public class StudentEntryEventConsumer : BackgroundService
{
    private readonly ILogger<StudentEntryEventConsumer> _logger;
    private readonly IConfiguration _configuration;
    private readonly IServiceProvider _serviceProvider;
    private RabbitMQConsumer? _consumer;

    public StudentEntryEventConsumer(
        ILogger<StudentEntryEventConsumer> logger,
        IConfiguration configuration,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _configuration = configuration;
        _serviceProvider = serviceProvider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(5000, stoppingToken); // RabbitMQ'nun başlamasını bekle

        var rabbitHost = _configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
        var rabbitUser = _configuration.GetValue<string>("RabbitMQ:Username") ?? "library";
        var rabbitPass = _configuration.GetValue<string>("RabbitMQ:Password") ?? "library123";

        _consumer = new RabbitMQConsumer(rabbitHost, rabbitUser, rabbitPass, "reservation_service_queue");

        _consumer.Subscribe<StudentEnteredEvent>("student.entered", HandleStudentEntry);

        _logger.LogInformation("StudentEntryEventConsumer started and listening for events");

        // Keep the service running
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }

    private void HandleStudentEntry(StudentEnteredEvent eventData)
    {
        _logger.LogInformation(
            "Student {StudentNumber} entered library at {EntryTime} via {TurnstileId}",
            eventData.StudentNumber,
            eventData.EntryTime,
            eventData.TurnstileId
        );

        // Rezervasyon IsAttended = true olarak işaretle (ceza sisteminin yanlış çalışmasını engeller)
        try
        {
            MarkReservationAttendedAsync(eventData).GetAwaiter().GetResult();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IsAttended güncellenirken hata oluştu. StudentNumber: {StudentNumber}", eventData.StudentNumber);
        }
    }

    private async Task MarkReservationAttendedAsync(StudentEnteredEvent evt)
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ReservationDbContext>();

        var entryTime = evt.EntryTime.ToLocalTime();
        var today = DateOnly.FromDateTime(entryTime);
        var nowTime = TimeOnly.FromDateTime(entryTime);

        // Öğrencinin bugünkü aktif rezervasyonunu bul (15 dakika tolerans ile)
        var reservation = await context.Reservations
            .Where(r => r.StudentNumber == evt.StudentNumber
                     && r.ReservationDate == today
                     && r.StartTime.AddMinutes(-10) <= nowTime
                     && r.EndTime >= nowTime
                     && !r.IsAttended)
            .FirstOrDefaultAsync();

        if (reservation != null)
        {
            reservation.IsAttended = true;
            await context.SaveChangesAsync();
            _logger.LogInformation(
                "Reservation {ReservationId} marked as attended for student {StudentNumber}",
                reservation.Id, evt.StudentNumber);
        }
        else
        {
            _logger.LogWarning(
                "No active reservation found for student {StudentNumber} at entry time {EntryTime}",
                evt.StudentNumber, entryTime);
        }
    }

    public override void Dispose()
    {
        _consumer?.Dispose();
        base.Dispose();
    }
}
