namespace TodoApi.Dtos;

public class UpdateTodoItem
{
    public required string Title { get; set; }
    public bool IsCompleted { get; set; }
    public string? Description { get; set; }
    public long TodoListId { get; set; }
}
