namespace TodoApi.Services;

public interface ITodoItemService
{
    Task MarkAllAsDoneAsync(int todoListId);
}