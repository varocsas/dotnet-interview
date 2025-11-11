using System;

namespace TodoApi.Dtos.ExternalApi;

public class CreateExternalTodoListDto
{
    public string Name { get; init; } = string.Empty;
}