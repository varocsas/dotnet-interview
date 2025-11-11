namespace TodoApi.Dtos;

public class CreateTodoItem
{
    public required string Title { get; set; }

    public string? Description { get; set; }
    
    public bool IsCompleted { get; set; }

    public long TodoListId { get; set; }
}
