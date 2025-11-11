using System;

namespace TodoApi.Dtos.ExternalApi;

public class ExternalTodoListDto
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

