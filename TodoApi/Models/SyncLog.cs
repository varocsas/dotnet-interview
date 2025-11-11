namespace TodoApi.Models;

public class SyncLog
{
    public int Id { get; set; }
    public string EntityType { get; set; } = string.Empty;
    public long? EntityId { get; set; }
    public string Operation { get; set; } = string.Empty; // "Create", "Update", "Delete"
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; }
    public int RetryCount { get; set; }
}