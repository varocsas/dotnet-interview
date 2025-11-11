using System;

namespace TodoApi.Dtos.ExternalApi;

public class ExternalTodoItemDto
{
    public long Id { get; init; }
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsCompleted { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}



