namespace TodoApi.Dtos;

public class TodoListDto
{
    public int Id { get; set; }
    public required string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public required IEnumerable<TodoItemDto> Items { get; set; }
}

