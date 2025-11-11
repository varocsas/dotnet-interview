using System.Threading.Tasks;
using System.Threading;

namespace TodoApi.Services.Sync;

public interface ISyncService
{
    Task<SyncResult> SyncAllAsync(CancellationToken cancellationToken = default);
    Task<SyncResult> SyncTodoListAsync(long localId, CancellationToken cancellationToken = default);
    Task<SyncResult> SyncTodoItemAsync(long localId, CancellationToken cancellationToken = default);
    Task<SyncStatus> GetSyncStatusAsync();
}