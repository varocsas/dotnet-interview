namespace TodoApi.Dtos;

public class TodoItemDto
{
    public TodoItemDto(int id, string? description, bool isCompleted, DateTime createdAt, DateTime updatedAt, long todoListId)
    {
        Id = id;
        Description = description;
        IsCompleted = isCompleted;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
        TodoListId = todoListId;
    }

    public int Id { get; set; }
    public string? Description { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public long TodoListId { get; set; }
}
