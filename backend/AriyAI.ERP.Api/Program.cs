using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using AriyAI.ERP.Api.Data;

// Load .env file from workspace root or current directory if it exists
var currentDir = Directory.GetCurrentDirectory();
var envPaths = new[]
{
    Path.Combine(currentDir, ".env"),
    Path.Combine(currentDir, "..", ".env"),
    Path.Combine(currentDir, "..", "..", ".env")
};

foreach (var path in envPaths)
{
    if (File.Exists(path))
    {
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;
            var parts = line.Split('=', 2);
            if (parts.Length == 2)
            {
                var envKey = parts[0].Trim();
                var envValue = parts[1].Trim().Trim('"').Trim('\''); // Strip optional quotes
                Environment.SetEnvironmentVariable(envKey, envValue);
            }
        }
        break; // Load first found .env
    }
}

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles;
    });

// Configure EF Core with SQLite
builder.Services.AddDbContext<ErpDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("DefaultConnection") ?? "Data Source=erp.db"));

// Register email sync, extraction and fuzzy catalog matching services
builder.Services.AddScoped<AriyAI.ERP.Api.Services.ExtractionService>();
builder.Services.AddScoped<AriyAI.ERP.Api.Services.MatchingService>();
builder.Services.AddScoped<AriyAI.ERP.Api.Filters.AgentAuthFilter>();
builder.Services.AddSingleton<AriyAI.ERP.Api.Services.EmailSyncWorker>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<AriyAI.ERP.Api.Services.EmailSyncWorker>());

// Configure CORS for Angular Frontend
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors("AllowAngular");

// Auto-migration & Database Seeding on startup
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<ErpDbContext>();
        DbInitializer.Initialize(context);
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "An error occurred while seeding the database.");
    }
}

app.MapControllers();

app.Run();
