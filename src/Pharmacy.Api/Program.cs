using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.IdentityModel.Tokens;
using Pharmacy.Api.Middleware;
using Pharmacy.Core.Interfaces;
using Pharmacy.Infrastructure.Data;
using Pharmacy.Infrastructure.Repositories;
using Pharmacy.Infrastructure.Services;
using System;
using System.Text;
using Serilog;

// Disable config file watching to avoid hitting the inotify instance limit
// on Linux hosts (e.g., Render free tier where the cap is 128).
var builder = WebApplication.CreateBuilder(new WebApplicationOptions
{
    Args = args
});
builder.Configuration.Sources.Clear();
builder.Configuration
    .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
    .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
    .AddEnvironmentVariables();

// Configure Serilog daily rotating logs
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.File("logs/ttyt-saidong-.log", rollingInterval: RollingInterval.Day, outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Add Services to the container.
builder.Services.AddControllers();
builder.Services.AddHttpClient();

// Configure EF Core with PostgreSQL (Supabase)
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection");
builder.Services.AddDbContext<PharmacyDbContext>(options =>
    options.UseNpgsql(connectionString));

// Register Repositories
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IProductRepository, ProductRepository>();
builder.Services.AddScoped<ICustomerRepository, CustomerRepository>();
builder.Services.AddScoped<IOrderRepository, OrderRepository>();

// Register Services
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IOrderService, OrderService>();
builder.Services.AddScoped<IReportService, ReportService>();

// Configure JWT Authentication
var secretKey = builder.Configuration["Jwt:Key"] ?? "TTYTSaiDongSuperSecretSecurityKeyThatIsAtLeast32BytesLong!";
var issuer = builder.Configuration["Jwt:Issuer"] ?? "TTYTSaiDongApi";
var audience = builder.Configuration["Jwt:Audience"] ?? "TTYTSaiDongClient";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
})
.AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
        ClockSkew = TimeSpan.Zero
    };
});

// Configure CORS for Angular Spa Client
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Configure Swagger/OpenAPI
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// Auto-migrate database on startup (Senior DevOps/DBA Best Practice)
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<PharmacyDbContext>();
        await context.Database.MigrateAsync();
        await DbInitializer.SeedAsync(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while running automated EF Core migrations.");
    }
}

// Enable Swagger UI in all environments to simplify local IIS testing
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", "TTYT Sai Dong API v1");
    c.RoutePrefix = "swagger"; // Available at http://localhost:<port>/swagger
});

app.UseMiddleware<ExceptionHandlingMiddleware>();

// Enable CORS
app.UseCors("AllowAngular");

app.UseAuthentication();
app.UseAuthorization();

// Serve Angular static files from wwwroot
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapControllers();
app.MapFallbackToFile("index.html");

app.Run();
