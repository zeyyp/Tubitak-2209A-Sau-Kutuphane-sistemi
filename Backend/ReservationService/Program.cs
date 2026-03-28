using System;
using Microsoft.EntityFrameworkCore;
using ReservationService.Data;
using ReservationService.Services;
using Shared.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<ReservationDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddHttpClient("IdentityService", client =>
{
    var baseAddress = builder.Configuration.GetValue<string>("Services:Identity") ?? "http://localhost:5001/";
    client.BaseAddress = new Uri(baseAddress);
    client.Timeout = TimeSpan.FromSeconds(10);
});

// RabbitMQ Publisher - Event yayınlamak için
builder.Services.AddSingleton(sp => 
{
    var config = sp.GetRequiredService<IConfiguration>();
    var rabbitHost = config["RabbitMQ:Host"] ?? "localhost";
    var rabbitUser = config["RabbitMQ:Username"] ?? "guest";
    var rabbitPass = config["RabbitMQ:Password"] ?? "guest";
    return new RabbitMQPublisher(rabbitHost, rabbitUser, rabbitPass);
});

// RabbitMQ Event Consumer
builder.Services.AddHostedService<StudentEntryEventConsumer>();

// Otomatik ceza kontrolü servisi - Her 1 dakikada bir çalışır
builder.Services.AddHostedService<PenaltyCheckService>();

// Priority Service - Puan ve erişim kontrolü
builder.Services.AddScoped<PriorityService>();

// CORS Politikası - ngrok için geçici olarak tüm origin'lere açık
builder.Services.AddCors(options =>
{
    options.AddPolicy("SecurePolicy", policy =>
    {
        policy.SetIsOriginAllowed(origin => true)  // Tüm origin'lere izin (ngrok için)
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();
    });
});

// HTTPS Zorunluluğu (Production için)
if (!builder.Environment.IsDevelopment())
{
    builder.Services.AddHsts(options =>
    {
        options.MaxAge = TimeSpan.FromDays(365);
        options.IncludeSubDomains = true;
        options.Preload = true;
    });
}

builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.Converters.Add(new ReservationService.Converters.DateOnlyJsonConverter());
        options.JsonSerializerOptions.Converters.Add(new ReservationService.Converters.TimeOnlyJsonConverter());
    });

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ReservationDbContext>();
        // Ensure database is created/migrated
        context.Database.Migrate();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred seeding the DB.");
    }
}

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    await next();
});

// HTTPS Redirect (Production)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseCors("SecurePolicy");

app.MapControllers();

app.MapGet("/", () => "Reservation Service is running...");

app.Run();
