using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace TodoApi.Services.Sync;

public class SyncBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<SyncBackgroundService> _logger;
    private readonly TimeSpan _syncInterval;

    public SyncBackgroundService(
        IServiceProvider serviceProvider,
        ILogger<SyncBackgroundService> logger,
        IConfiguration configuration)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        
        var intervalMinutes = configuration.GetValue<int>("SyncSettings:IntervalMinutes", 5);
        _syncInterval = TimeSpan.FromMinutes(intervalMinutes);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation(
            "Sync Background Service starting with interval: {Interval}",
            _syncInterval
        );

        // Wait a bit before starting first sync to allow app to fully initialize
        await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogInformation("Starting scheduled synchronization");
                
                using var scope = _serviceProvider.CreateScope();
                var syncService = scope.ServiceProvider.GetRequiredService<ISyncService>();
                
                var result = await syncService.SyncAllAsync(stoppingToken);
                
                _logger.LogInformation(
                    "Scheduled sync completed: {EntitiesSynced} synced, {Errors} errors, Duration: {Duration}ms",
                    result.EntitiesSynced,
                    result.ErrorCount,
                    result.Duration.TotalMilliseconds
                );
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Sync cancelled due to shutdown");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during scheduled synchronization");
            }

            // Calculate next sync time
            var elapsed = DateTime.UtcNow - startTime;
            var delay = _syncInterval - elapsed;
            
            if (delay > TimeSpan.Zero)
            {
                _logger.LogDebug("Next sync in {Delay}", delay);
                await Task.Delay(delay, stoppingToken);
            }
            else
            {
                _logger.LogWarning(
                    "Sync took longer than interval ({Elapsed} > {Interval}), starting next sync immediately",
                    elapsed,
                    _syncInterval
                );
            }
        }

        _logger.LogInformation("Sync Background Service stopped");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Sync Background Service stopping...");
        await base.StopAsync(cancellationToken);
    }
}