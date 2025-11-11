using TodoApi.Dtos.ExternalApi;

namespace TodoApi.Services.ExternalApi;

public interface IExternalTodoApiClient
{
    // TodoList operations
    Task<IEnumerable<ExternalTodoListDto>> GetTodoListsAsync();
    Task<ExternalTodoListDto> GetTodoListAsync(long id);
    Task<ExternalTodoListDto> CreateTodoListAsync(CreateExternalTodoListDto dto);
    Task UpdateTodoListAsync(long id, UpdateExternalTodoListDto dto);
    Task DeleteTodoListAsync(long id);

    // TodoItem operations
    Task<IEnumerable<ExternalTodoItemDto>> GetTodoItemsAsync(long todoListId);
    Task<ExternalTodoItemDto> GetTodoItemAsync(long todoListId, long itemId);
    Task<ExternalTodoItemDto> CreateTodoItemAsync(long todoListId, CreateExternalTodoItemDto dto);
    Task UpdateTodoItemAsync(long todoListId, long itemId, UpdateExternalTodoItemDto dto);
    Task DeleteTodoItemAsync(long todoListId, long itemId);
}