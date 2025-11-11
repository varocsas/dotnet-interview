using System;
using System.Collections.Generic;

namespace TodoApi.Services.Sync;

public class SyncResult
{
    public bool Success { get; set; }
    public int EntitiesSynced { get; set; }
    public int ErrorCount { get; set; }
    public List<string> Errors { get; set; } = new();
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public TimeSpan Duration => CompletedAt - StartedAt;
}