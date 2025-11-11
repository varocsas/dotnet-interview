using System;

namespace TodoApi.Dtos.ExternalApi;

public class CreateExternalTodoItemDto
{
    public string Title { get; init; } = string.Empty;
    public string? Description { get; init; }
    public bool IsCompleted { get; init; }
}