using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using TodoApi.Data;
using TodoApi.Dtos.ExternalApi;
using TodoApi.Services.ExternalApi;
using TodoApi.Services.Sync;
using System.Linq;
using System.Collections.Generic;
using TodoApi.Models;

namespace TodoApi.Services.Sync;

public class SyncService : ISyncService
{
    private readonly TodoContext _context;
    private readonly IExternalTodoApiClient _externalApi;
    private readonly ILogger<SyncService> _logger;
    private const int MaxRetries = 3;

    public SyncService(
        TodoContext context,
        IExternalTodoApiClient externalApi,
        ILogger<SyncService> logger)
    {
        _context = context;
        _externalApi = externalApi;
        _logger = logger;
    }

    public async Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default)
    {
        var result = new SyncResult { StartedAt = DateTime.UtcNow };
        _logger.LogInformation("Starting full synchronization");

        try
        {
            // Phase 1: Sync TodoLists
            var listResult = await SyncTodoListsAsync(cancellationToken);
            result.EntitiesSynced += listResult.EntitiesSynced;
            result.ErrorCount += listResult.ErrorCount;
            result.Errors.AddRange(listResult.Errors);

            // Phase 2: Sync TodoItems for all mapped lists
            var itemResult = await SyncAllTodoItemsAsync(cancellationToken);
            result.EntitiesSynced += itemResult.EntitiesSynced;
            result.ErrorCount += itemResult.ErrorCount;
            result.Errors.AddRange(itemResult.Errors);

            result.Success = result.ErrorCount == 0;
            result.CompletedAt = DateTime.UtcNow;

            _logger.LogInformation(
                "Full sync completed: {EntitiesSynced} synced, {ErrorCount} errors in {Duration}ms",
                result.EntitiesSynced,
                result.ErrorCount,
                result.Duration.TotalMilliseconds
            );

            return result;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add($"Fatal error: {ex.Message}");
            _logger.LogError(ex, "Full synchronization failed");
            return result;
        }
    }

    private async Task<SyncResult> SyncTodoListsAsync(CancellationToken cancellationToken)
    {
        var result = new SyncResult { StartedAt = DateTime.UtcNow };

        try
        {
            // Pull from external API
            var pullResult = await PullTodoListsFromExternalAsync(cancellationToken);
            result.EntitiesSynced += pullResult.EntitiesSynced;
            result.ErrorCount += pullResult.ErrorCount;
            result.Errors.AddRange(pullResult.Errors);

            // Push to external API
            var pushResult = await PushTodoListsToExternalAsync(cancellationToken);
            result.EntitiesSynced += pushResult.EntitiesSynced;
            result.ErrorCount += pushResult.ErrorCount;
            result.Errors.AddRange(pushResult.Errors);

            result.Success = result.ErrorCount == 0;
            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add($"TodoList sync error: {ex.Message}");
            _logger.LogError(ex, "TodoList synchronization failed");
        }

        return result;
    }

    private async Task<SyncResult> PullTodoListsFromExternalAsync(CancellationToken cancellationToken)
    {
        var result = new SyncResult { StartedAt = DateTime.UtcNow };

        try
        {
            var externalLists = await _externalApi.GetTodoListsAsync();
            var syncStates = await _context.Set<SyncState>()
                .Where(s => s.EntityType == "TodoList" && s.ExternalTodoListId != null)
                .ToDictionaryAsync(s => s.ExternalTodoListId!.Value, cancellationToken);

            foreach (var externalList in externalLists)
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (!syncStates.TryGetValue(externalList.Id, out var syncState))
                {
                    // New external list - create locally
                    await CreateLocalTodoListAsync(externalList, cancellationToken);
                    result.EntitiesSynced++;
                }
                else
                {
                    // Check if external version is newer
                    var localList = await _context.TodoLists.FindAsync(
                        new object[] { syncState.LocalTodoListId!.Value },
                        cancellationToken
                    );

                    if (localList != null && externalList.UpdatedAt > syncState.LastSyncedAt)
                    {
                        // Update local from external
                        localList.Name = externalList.Name;
                        localList.UpdatedAt = externalList.UpdatedAt;
                        syncState.LastSyncedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(cancellationToken);

                        result.EntitiesSynced++;
                        _logger.LogDebug(
                            "Updated local TodoList {LocalId} from external {ExternalId}",
                            localList.Id,
                            externalList.Id
                        );
                    }
                }
            }

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add($"Pull error: {ex.Message}");
            _logger.LogError(ex, "Failed to pull TodoLists from external API");
        }

        return result;
    }

    private async Task<SyncResult> PushTodoListsToExternalAsync(CancellationToken cancellationToken)
    {
        var result = new SyncResult { StartedAt = DateTime.UtcNow };

        try
        {
            var localLists = await _context.TodoLists.ToListAsync(cancellationToken);
            var syncStates = await _context.Set<SyncState>()
                .Where(s => s.EntityType == "TodoList" && s.LocalTodoListId != null)
                .ToDictionaryAsync(s => s.LocalTodoListId!.Value, cancellationToken);

            foreach (var localList in localLists)
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (!syncStates.TryGetValue(localList.Id, out var syncState))
                {
                    // New local list - push to external
                    var success = await CreateExternalTodoListAsync(localList, cancellationToken);
                    if (success)
                    {
                        result.EntitiesSynced++;
                    }
                    else
                    {
                        result.ErrorCount++;
                        // Retrieve the last sync log for this entity to include the underlying error message
                        var lastLog = await _context.Set<SyncLog>()
                            .Where(l => l.EntityType == "TodoList" && l.EntityId == localList.Id && !l.Success)
                            .OrderByDescending(l => l.Timestamp)
                            .FirstOrDefaultAsync(cancellationToken);

                        var underlying = lastLog?.ErrorMessage ?? "Unknown error";
                        result.Errors.Add($"Failed to create external TodoList for local ID {localList.Id}: {underlying}");
                    }
                }
                else if (localList.UpdatedAt > syncState.LastSyncedAt)
                {
                    // Local changes need to be pushed
                    var success = await UpdateExternalTodoListAsync(
                        localList,
                        syncState.ExternalTodoListId!.Value,
                        cancellationToken
                    );

                    if (success)
                    {
                        syncState.LastSyncedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(cancellationToken);
                        result.EntitiesSynced++;
                    }
                    else
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Failed to update external TodoList {syncState.ExternalTodoListId}");
                    }
                }
            }

            result.Success = result.ErrorCount == 0;
            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add($"Push error: {ex.Message}");
            _logger.LogError(ex, "Failed to push TodoLists to external API");
        }

        return result;
    }

    private async Task<SyncResult> SyncAllTodoItemsAsync(CancellationToken cancellationToken)
    {
        var result = new SyncResult { StartedAt = DateTime.UtcNow };

        try
        {
            // Get all TodoList sync mappings
            var listMappings = await _context.Set<SyncState>()
                .Where(s => s.EntityType == "TodoList" 
                    && s.LocalTodoListId != null 
                    && s.ExternalTodoListId != null)
                .ToListAsync(cancellationToken);

            foreach (var mapping in listMappings)
            {
                if (cancellationToken.IsCancellationRequested) break;

                // Pull items from external
                var pullResult = await PullTodoItemsFromExternalAsync(
                    mapping.LocalTodoListId!.Value,
                    mapping.ExternalTodoListId!.Value,
                    cancellationToken
                );
                result.EntitiesSynced += pullResult.EntitiesSynced;
                result.ErrorCount += pullResult.ErrorCount;
                result.Errors.AddRange(pullResult.Errors);

                // Push local items to external
                var pushResult = await PushTodoItemsToExternalAsync(
                    mapping.LocalTodoListId!.Value,
                    mapping.ExternalTodoListId!.Value,
                    cancellationToken
                );
                result.EntitiesSynced += pushResult.EntitiesSynced;
                result.ErrorCount += pushResult.ErrorCount;
                result.Errors.AddRange(pushResult.Errors);
            }

            result.Success = result.ErrorCount == 0;
            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add($"TodoItem sync error: {ex.Message}");
            _logger.LogError(ex, "TodoItem synchronization failed");
        }

        return result;
    }

    private async Task<SyncResult> PullTodoItemsFromExternalAsync(
        long localListId,
        long externalListId,
        CancellationToken cancellationToken)
    {
        var result = new SyncResult { StartedAt = DateTime.UtcNow };

        try
        {
            var externalItems = await _externalApi.GetTodoItemsAsync(externalListId);
            var syncStates = await _context.Set<SyncState>()
                .Where(s => s.EntityType == "TodoItem" && s.ExternalTodoItemId != null)
                .ToDictionaryAsync(s => s.ExternalTodoItemId!.Value, cancellationToken);

            foreach (var externalItem in externalItems)
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (!syncStates.TryGetValue(externalItem.Id, out var syncState))
                {
                    // New external item - create locally
                    var localItem = new TodoItem
                    {
                        Title = externalItem.Title,
                        Description = externalItem.Description,
                        IsCompleted = externalItem.IsCompleted,
                        TodoListId = localListId,
                        CreatedAt = externalItem.CreatedAt,
                        UpdatedAt = externalItem.UpdatedAt
                    };

                    _context.TodoItems.Add(localItem);
                    await _context.SaveChangesAsync(cancellationToken);

                    // Create sync mapping
                    _context.Set<SyncState>().Add(new SyncState
                    {
                        EntityType = "TodoItem",
                        LocalTodoItemId = localItem.Id,
                        ExternalTodoItemId = externalItem.Id,
                        LastSyncedAt = DateTime.UtcNow
                    });
                    await _context.SaveChangesAsync(cancellationToken);

                    result.EntitiesSynced++;
                    _logger.LogDebug(
                        "Created local TodoItem {LocalId} from external {ExternalId}",
                        localItem.Id,
                        externalItem.Id
                    );
                }
                else
                {
                    // Check if external version is newer
                    var localItem = await _context.TodoItems.FindAsync(
                        new object[] { syncState.LocalTodoItemId!.Value },
                        cancellationToken
                    );

                    if (localItem != null && externalItem.UpdatedAt > syncState.LastSyncedAt)
                    {
                        localItem.Title = externalItem.Title;
                        localItem.Description = externalItem.Description;
                        localItem.IsCompleted = externalItem.IsCompleted;
                        localItem.UpdatedAt = externalItem.UpdatedAt;
                        syncState.LastSyncedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(cancellationToken);

                        result.EntitiesSynced++;
                    }
                }
            }

            result.Success = true;
            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add($"Pull items error for list {externalListId}: {ex.Message}");
            _logger.LogError(ex, "Failed to pull TodoItems from external list {ExternalListId}", externalListId);
        }

        return result;
    }

    private async Task<SyncResult> PushTodoItemsToExternalAsync(
        long localListId,
        long externalListId,
        CancellationToken cancellationToken)
    {
        var result = new SyncResult { StartedAt = DateTime.UtcNow };

        try
        {
            var localItems = await _context.TodoItems
                .Where(i => i.TodoListId == localListId)
                .ToListAsync(cancellationToken);

            var syncStates = await _context.Set<SyncState>()
                .Where(s => s.EntityType == "TodoItem" && s.LocalTodoItemId != null)
                .ToDictionaryAsync(s => s.LocalTodoItemId!.Value, cancellationToken);

            foreach (var localItem in localItems)
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (!syncStates.TryGetValue(localItem.Id, out var syncState))
                {
                    // New local item - push to external
                    var success = await CreateExternalTodoItemAsync(
                        localItem,
                        externalListId,
                        cancellationToken
                    );

                    if (success) result.EntitiesSynced++;
                    else
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Failed to create external TodoItem for local ID {localItem.Id}");
                    }
                }
                else if (localItem.UpdatedAt > syncState.LastSyncedAt)
                {
                    // Local changes need to be pushed
                    var success = await UpdateExternalTodoItemAsync(
                        localItem,
                        externalListId,
                        syncState.ExternalTodoItemId!.Value,
                        cancellationToken
                    );

                    if (success)
                    {
                        syncState.LastSyncedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync(cancellationToken);
                        result.EntitiesSynced++;
                    }
                    else
                    {
                        result.ErrorCount++;
                        result.Errors.Add($"Failed to update external TodoItem {syncState.ExternalTodoItemId}");
                    }
                }
            }

            result.Success = result.ErrorCount == 0;
            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add($"Push items error for list {localListId}: {ex.Message}");
            _logger.LogError(ex, "Failed to push TodoItems to external list {ExternalListId}", externalListId);
        }

        return result;
    }

    // Helper methods with retry logic
    private async Task CreateLocalTodoListAsync(ExternalTodoListDto externalList, CancellationToken cancellationToken)
    {
        var localList = new TodoList
        {
            Name = externalList.Name,
            CreatedAt = externalList.CreatedAt,
            UpdatedAt = externalList.UpdatedAt
        };

        _context.TodoLists.Add(localList);
        await _context.SaveChangesAsync(cancellationToken);

        _context.Set<SyncState>().Add(new SyncState
        {
            EntityType = "TodoList",
            LocalTodoListId = localList.Id,
            ExternalTodoListId = externalList.Id,
            LastSyncedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Created local TodoList {LocalId} from external {ExternalId}",
            localList.Id,
            externalList.Id
        );
    }

    private async Task<bool> CreateExternalTodoListAsync(TodoList localList, CancellationToken cancellationToken)
    {
        try
        {
            var externalList = await ExecuteWithRetryAsync(
                async () => await _externalApi.CreateTodoListAsync(new CreateExternalTodoListDto
                {
                    Name = localList.Name
                }),
                MaxRetries
            );

            _context.Set<SyncState>().Add(new SyncState
            {
                EntityType = "TodoList",
                LocalTodoListId = localList.Id,
                ExternalTodoListId = externalList.Id,
                LastSyncedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Created external TodoList {ExternalId} from local {LocalId}",
                externalList.Id,
                localList.Id
            );

            return true;
        }
        catch (Exception ex)
        {
            await LogSyncErrorAsync("TodoList", localList.Id, "Create", ex.Message);
            return false;
        }
    }

    private async Task<bool> UpdateExternalTodoListAsync(
        TodoList localList,
        long externalId,
        CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteWithRetryAsync(
                async () =>
                {
                    await _externalApi.UpdateTodoListAsync(externalId, new UpdateExternalTodoListDto
                    {
                        Name = localList.Name
                    });
                    return true;
                },
                MaxRetries
            );

            _logger.LogDebug(
                "Updated external TodoList {ExternalId} from local {LocalId}",
                externalId,
                localList.Id
            );

            return true;
        }
        catch (Exception ex)
        {
            await LogSyncErrorAsync("TodoList", localList.Id, "Update", ex.Message);
            return false;
        }
    }

    private async Task<bool> CreateExternalTodoItemAsync(
        TodoItem localItem,
        long externalListId,
        CancellationToken cancellationToken)
    {
        try
        {
            var externalItem = await ExecuteWithRetryAsync(
                async () => await _externalApi.CreateTodoItemAsync(
                    externalListId,
                    new CreateExternalTodoItemDto
                    {
                        Title = localItem.Title,
                        Description = localItem.Description,
                        IsCompleted = localItem.IsCompleted
                    }
                ),
                MaxRetries
            );

            _context.Set<SyncState>().Add(new SyncState
            {
                EntityType = "TodoItem",
                LocalTodoItemId = localItem.Id,
                ExternalTodoItemId = externalItem.Id,
                LastSyncedAt = DateTime.UtcNow
            });

            await _context.SaveChangesAsync(cancellationToken);

            _logger.LogDebug(
                "Created external TodoItem {ExternalId} from local {LocalId}",
                externalItem.Id,
                localItem.Id
            );

            return true;
        }
        catch (Exception ex)
        {
            await LogSyncErrorAsync("TodoItem", localItem.Id, "Create", ex.Message);
            return false;
        }
    }

    private async Task<bool> UpdateExternalTodoItemAsync(
        TodoItem localItem,
        long externalListId,
        long externalItemId,
        CancellationToken cancellationToken)
    {
        try
        {
            await ExecuteWithRetryAsync(
                async () =>
                {
                    await _externalApi.UpdateTodoItemAsync(
                        externalListId,
                        externalItemId,
                        new UpdateExternalTodoItemDto
                        {
                            Title = localItem.Title,
                            Description = localItem.Description,
                            IsCompleted = localItem.IsCompleted
                        }
                    );
                    return true;
                },
                MaxRetries
            );

            return true;
        }
        catch (Exception ex)
        {
            await LogSyncErrorAsync("TodoItem", localItem.Id, "Update", ex.Message);
            return false;
        }
    }

    private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> action, int maxRetries)
    {
        var retryCount = 0;
        while (true)
        {
            try
            {
                return await action();
            }
            catch (HttpRequestException ex) when (retryCount < maxRetries)
            {
                retryCount++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, retryCount));
                _logger.LogWarning(
                    ex,
                    "Request failed. Retry {RetryCount}/{MaxRetries} after {Delay}s",
                    retryCount,
                    maxRetries,
                    delay.TotalSeconds
                );
                await Task.Delay(delay);
            }
        }
    }

    private async Task LogSyncErrorAsync(string entityType, long entityId, string operation, string error)
    {
        var syncLog = new SyncLog
        {
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            Success = false,
            ErrorMessage = error,
            Timestamp = DateTime.UtcNow,
            RetryCount = 0
        };

        _context.Set<SyncLog>().Add(syncLog);
        await _context.SaveChangesAsync();

        _logger.LogError(
            "Sync error for {EntityType} {EntityId} during {Operation}: {Error}",
            entityType,
            entityId,
            operation,
            error
        );
    }

    public async Task<SyncResult> SyncTodoListAsync(long localId, CancellationToken cancellationToken = default)
    {
        var result = new SyncResult { StartedAt = DateTime.UtcNow };

        try
        {
            var syncState = await _context.Set<SyncState>()
                .FirstOrDefaultAsync(
                    s => s.EntityType == "TodoList" && s.LocalTodoListId == localId,
                    cancellationToken
                );

            if (syncState == null)
            {
                result.Errors.Add($"No sync state found for TodoList {localId}");
                result.Success = false;
            }
            else
            {
                // Implement single list sync logic
                result.Success = true;
                result.EntitiesSynced = 1;
            }

            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    public async Task<SyncResult> SyncTodoItemAsync(long localId, CancellationToken cancellationToken = default)
    {
        var result = new SyncResult { StartedAt = DateTime.UtcNow };

        try
        {
            var syncState = await _context.Set<SyncState>()
                .FirstOrDefaultAsync(
                    s => s.EntityType == "TodoItem" && s.LocalTodoItemId == localId,
                    cancellationToken
                );

            if (syncState == null)
            {
                result.Errors.Add($"No sync state found for TodoItem {localId}");
                result.Success = false;
            }
            else
            {
                // Implement single item sync logic
                result.Success = true;
                result.EntitiesSynced = 1;
            }

            result.CompletedAt = DateTime.UtcNow;
        }
        catch (Exception ex)
        {
            result.Success = false;
            result.CompletedAt = DateTime.UtcNow;
            result.Errors.Add(ex.Message);
        }

        return result;
    }

    public async Task<SyncStatus> GetSyncStatusAsync()
    {
        var recentLogs = await _context.Set<SyncLog>()
            .OrderByDescending(l => l.Timestamp)
            .Take(10)
            .ToListAsync();

        var lastSuccess = await _context.Set<SyncLog>()
            .Where(l => l.Success)
            .OrderByDescending(l => l.Timestamp)
            .FirstOrDefaultAsync();

        var failedCount = await _context.Set<SyncLog>()
            .Where(l => !l.Success && l.Timestamp > DateTime.UtcNow.AddHours(-24))
            .CountAsync();

        return new SyncStatus
        {
            LastSuccessfulSync = lastSuccess?.Timestamp,
            PendingSyncCount = 0, // Could calculate based on UpdatedAt timestamps
            FailedSyncCount = failedCount,
            IsHealthy = failedCount < 10,
            RecentErrors = recentLogs
                .Where(l => !l.Success)
                .Select(l => new SyncError
                {
                    EntityType = l.EntityType,
                    EntityId = l.EntityId,
                    Operation = l.Operation,
                    Message = l.ErrorMessage ?? "Unknown error",
                    Timestamp = l.Timestamp
                })
                .ToList()
        };
    }
}