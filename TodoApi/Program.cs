using Hangfire;
using Hangfire.Dashboard;
using Hangfire.SqlServer;
using Hangfire.MemoryStorage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using TodoApi.Data;
using TodoApi.Services;
using TodoApi.Services.ExternalApi;
using TodoApi.Services.Sync;
using Polly;
using Polly.Extensions.Http;

namespace TodoApi;

public partial class Program 
{ 
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

// Add DbContext
    builder.Services.AddDbContext<TodoContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection"),
            sqlOptions =>
            {
                sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 3,
                    maxRetryDelay: TimeSpan.FromSeconds(5),
                    errorNumbersToAdd: null
                );
                sqlOptions.CommandTimeout(30);
            }
        )
);

// Configure External API Settings
builder.Services.Configure<ExternalApiSettings>(
    builder.Configuration.GetSection("ExternalApiSettings")
);

// Add HttpClient for External API
builder.Services.AddHttpClient<IExternalTodoApiClient, ExternalTodoApiClient>()
    .SetHandlerLifetime(TimeSpan.FromMinutes(5))
    .AddPolicyHandler(GetRetryPolicy())
    .AddPolicyHandler(GetCircuitBreakerPolicy());

// Add Services
builder.Services.AddScoped<ITodoItemService, TodoItemService>();
builder.Services.AddScoped<ISyncService, SyncService>();

// Configure Hangfire: try SQL Server storage first, fall back to in-memory if SQL isn't reachable
var hangfireConnectionString = builder.Configuration.GetConnectionString("HangfireConnection");
var hangfireUseSql = false;
try
{
    // Try opening a short-timeout SQL connection to verify availability
    var csb = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(hangfireConnectionString);
    if (csb.ConnectTimeout == 0) csb.ConnectTimeout = 5;
    using var _testConn = new Microsoft.Data.SqlClient.SqlConnection(csb.ConnectionString);
    _testConn.Open();
    hangfireUseSql = true;
}
catch (Exception ex)
{
    // Cannot reach SQL for Hangfire; fall back to memory storage (suitable for tests)
    Console.WriteLine($"Hangfire SQL storage not available: {ex.Message}. Falling back to in-memory storage.");
}

if (hangfireUseSql)
{
    builder.Services.AddHangfire(config =>
    {
        config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseSqlServerStorage(
                hangfireConnectionString,
                new SqlServerStorageOptions
                {
                    CommandBatchMaxTimeout = TimeSpan.FromMinutes(5),
                    SlidingInvisibilityTimeout = TimeSpan.FromMinutes(5),
                    QueuePollInterval = TimeSpan.Zero,
                    UseRecommendedIsolationLevel = true,
                    DisableGlobalLocks = true
                }
            );
    });
}
else
{
    // In-memory storage is fine for local development/testing when SQL isn't available
    builder.Services.AddHangfire(config =>
    {
        config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
            .UseSimpleAssemblyNameTypeSerializer()
            .UseRecommendedSerializerSettings()
            .UseMemoryStorage();
    });
}

builder.Services.AddHangfireServer(options =>
{
    options.WorkerCount = Environment.ProcessorCount * 2;
});

// Add Background Services
builder.Services.AddHostedService<SyncBackgroundService>();

// Add Controllers and API features
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Todo API with Synchronization",
        Version = "v1",
        Description = "A Todo API with external synchronization capabilities"
    });
});

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// Add Health Checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<TodoContext>("database")
    .AddUrlGroup(
        new Uri(builder.Configuration["ExternalApiSettings:BaseUrl"] ?? "https://example.com"),
        name: "external-api",
        timeout: TimeSpan.FromSeconds(10)
    );

var app = builder.Build();

// Configure middleware pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Todo API v1");
    });
}

// Run migrations/ensure database schema in Development (helpful for tests/dev when migrations may be partial)
if (app.Environment.IsDevelopment())
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<TodoContext>();
    try
    {
        db.Database.Migrate();
    }
    catch (Exception ex)
    {
        // If migrations cannot be applied for any reason, fall back to EnsureCreated to get a usable schema
        Console.WriteLine($"Database migrate failed: {ex.Message}. Falling back to EnsureCreated.");
    }

    // EnsureCreated will create any missing tables for the current model (useful for local dev/tests)
    try
    {
        db.Database.EnsureCreated();
    }
    catch (Exception ex)
    {
        Console.WriteLine($"EnsureCreated failed: {ex.Message}");
    }
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");

// Hangfire Dashboard
app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = new[] { new HangfireAuthorizationFilter() }
});

app.UseAuthorization();
app.MapControllers();
app.MapHealthChecks("/health");

        app.Run();
    }

    // Helper methods for Polly policies
    static IAsyncPolicy<HttpResponseMessage> GetRetryPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .WaitAndRetryAsync(
                3,
                retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt))
            );
    }

    static IAsyncPolicy<HttpResponseMessage> GetCircuitBreakerPolicy()
    {
        return HttpPolicyExtensions
            .HandleTransientHttpError()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: 5,
                durationOfBreak: TimeSpan.FromSeconds(30)
            );
    }
}

// Hangfire Authorization Filter (for development - use proper auth in production)
public class HangfireAuthorizationFilter : IDashboardAuthorizationFilter
{
    public bool Authorize(DashboardContext context)
    {
        // In production, implement proper authorization
        return true;
    }
}
