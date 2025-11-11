using System;

namespace TodoApi.Dtos.ExternalApi;

public class UpdateExternalTodoItemDto
{
    public string? Title { get; init; }
    public string? Description { get; init; }
    public bool? IsCompleted { get; init; }
}