using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TodoApi.Data;

namespace TodoApi.Services;

public class TodoItemService : ITodoItemService
{
    private readonly TodoContext _context;
    private readonly ILogger<TodoItemService> _logger;

    public TodoItemService(TodoContext context, ILogger<TodoItemService> logger)
    {
        _context = context;
        _logger = logger;
    }

    public async Task MarkAllAsDoneAsync(int todoListId)
    {
        _logger.LogInformation("Starting mark all as done for TodoList {TodoListId}", todoListId);

        var items = await _context.TodoItems
            .Where(i => i.TodoListId == todoListId && !i.IsCompleted)
            .ToListAsync();

        if (items.Count == 0)
        {
            _logger.LogInformation("No incomplete items found for TodoList {TodoListId}", todoListId);
            return;
        }

        foreach (var item in items)
        {
            item.IsCompleted = true;
            item.UpdatedAt = DateTime.UtcNow;
        }

        await _context.SaveChangesAsync();

        _logger.LogInformation(
            "Marked {Count} items as done for TodoList {TodoListId}",
            items.Count,
            todoListId
        );
    }
}