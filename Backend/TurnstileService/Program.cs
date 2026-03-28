using Microsoft.Extensions.Options;
using TurnstileService.Models;
using TurnstileService.Services;
using Shared.Events;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<TurnstileOptions>(builder.Configuration.GetSection("Turnstile"));
builder.Services.AddSingleton<ITurnstileEntryLog, InMemoryTurnstileEntryLog>();
builder.Services.AddSingleton<TurnstileAuthProvider>();

// RabbitMQ Publisher
builder.Services.AddSingleton<RabbitMQPublisher>(sp =>
{
    var rabbitHost = builder.Configuration.GetValue<string>("RabbitMQ:Host") ?? "localhost";
    var rabbitUser = builder.Configuration.GetValue<string>("RabbitMQ:Username") ?? "library";
    var rabbitPass = builder.Configuration.GetValue<string>("RabbitMQ:Password") ?? "library123";
    return new RabbitMQPublisher(rabbitHost, rabbitUser, rabbitPass);
});

builder.Services.AddHttpClient<ReservationAccessClient>((serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TurnstileOptions>>();
    client.BaseAddress = new Uri(options.Value.ReservationServiceBaseUrl);
    client.Timeout = TimeSpan.FromSeconds(5);
});

builder.Services.AddHttpClient("IdentityAuth", (serviceProvider, client) =>
{
    var options = serviceProvider.GetRequiredService<IOptions<TurnstileOptions>>();
    var baseUrl = options.Value.IdentityBaseUrl?.TrimEnd('/') ?? string.Empty;

    if (string.IsNullOrWhiteSpace(baseUrl))
    {
        throw new InvalidOperationException("Turnstile IdentityBaseUrl must be configured.");
    }

    client.BaseAddress = new Uri(baseUrl + "/");
    client.Timeout = TimeSpan.FromSeconds(10);
});

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

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

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

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.MapControllers();

app.MapGet("/", () => "Turnstile Service is running...");

app.MapGet("/health", () => Results.Ok(new
{
    service = "Turnstile",
    status = "Healthy",
    timestamp = DateTime.UtcNow
}));

app.Run();
