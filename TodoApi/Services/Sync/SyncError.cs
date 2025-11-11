using System;

namespace TodoApi.Services.Sync;

public class SyncError
{
    public string EntityType { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
}