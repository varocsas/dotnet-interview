using System;
using System.Collections.Generic;

namespace TodoApi.Services.Sync;

public class SyncStatus
{
    public DateTime? LastSuccessfulSync { get; set; }
    public int PendingSyncCount { get; set; }
    public int FailedSyncCount { get; set; }
    public bool IsHealthy { get; set; }
    public List<SyncError> RecentErrors { get; set; } = new();
}