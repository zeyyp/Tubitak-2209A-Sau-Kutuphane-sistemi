using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Ocelot.DependencyInjection;
using Ocelot.Middleware;

var builder = WebApplication.CreateBuilder(args);

// Docker ortamında ocelot.docker.json, yerel ortamda ocelot.json kullan
var environment = builder.Environment.EnvironmentName;
var ocelotConfigFile = environment == "Docker" ? "ocelot.docker.json" : "ocelot.json";

// ASPNETCORE_ENVIRONMENT=Development ama Docker'da çalışıyorsak container hostname'lerini kullan
var isDocker = Environment.GetEnvironmentVariable("DOTNET_RUNNING_IN_CONTAINER") == "true" ||
               File.Exists("/.dockerenv");
if (isDocker)
{
    ocelotConfigFile = "ocelot.docker.json";
}

builder.Configuration.AddJsonFile(ocelotConfigFile, optional: false, reloadOnChange: true);
builder.Services.AddOcelot(builder.Configuration);

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


var app = builder.Build();

// Security Headers
app.Use(async (context, next) =>
{
    context.Response.Headers.Append("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Append("X-Frame-Options", "DENY");
    context.Response.Headers.Append("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Append("Referrer-Policy", "strict-origin-when-cross-origin");
    context.Response.Headers.Append("Permissions-Policy", "geolocation=(), microphone=(), camera=()");
    await next();
});

// HTTPS Redirect (Production)
if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
    app.UseHsts();
}

app.UseCors("SecurePolicy");

app.MapGet("/", () => "API Gateway is running...").WithMetadata(new AllowAnonymousAttribute());

app.MapGet("/health", () => Results.Ok(new
{
    service = "ApiGateway",
    status = "Healthy",
    timestamp = DateTime.UtcNow
})).WithMetadata(new AllowAnonymousAttribute());

app.MapGet("/routes", (IConfiguration configuration) =>
{
    var routes = configuration.GetSection("Routes")
        .GetChildren()
        .Select(route => new
        {
            Upstream = route["UpstreamPathTemplate"],
            Downstream = route["DownstreamPathTemplate"],
            Methods = route.GetSection("UpstreamHttpMethod")
                .GetChildren()
                .Select(method => method.Value)
                .ToArray(),
            DownstreamPorts = route.GetSection("DownstreamHostAndPorts")
                .GetChildren()
                .Select(port => port["Port"])
                .ToArray()
        });

    return Results.Ok(routes);
}).WithMetadata(new AllowAnonymousAttribute());

await app.UseOcelot();

app.Run();
