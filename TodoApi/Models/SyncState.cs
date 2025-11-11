namespace TodoApi.Models;

public class SyncState
{
    public int Id { get; set; }
    public long? LocalTodoListId { get; set; }
    public long? ExternalTodoListId { get; set; }
    public long? LocalTodoItemId { get; set; }
    public long? ExternalTodoItemId { get; set; }
    public DateTime LastSyncedAt { get; set; }
    public string EntityType { get; set; } = string.Empty; // "TodoList" or "TodoItem"
}