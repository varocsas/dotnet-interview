using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Hangfire;
using TodoApi.Dtos;
using TodoApi.Models;
using TodoApi.Services;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TodoApi.Data;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/todolists/{todoListId}/items")]
public class TodoItemsController : ControllerBase
{
    private readonly TodoContext _context;
    private readonly ILogger<TodoItemsController> _logger;

    public TodoItemsController(TodoContext context, ILogger<TodoItemsController> logger)
    {
        _context = context;
        _logger = logger;
    }

    // GET: api/todolists/{todoListId}/items
    [HttpGet]
    public async Task<ActionResult<IEnumerable<TodoItemDto>>> GetTodoItems(int todoListId)
    {
        var todoList = await _context.TodoLists
            .Include(tl => tl.Items)
            .FirstOrDefaultAsync(tl => tl.Id == todoListId);

        if (todoList == null)
            return NotFound(new { message = $"TodoList with id {todoListId} not found" });

        return Ok(todoList.Items.Select(MapToDto));
    }

    // GET: api/todolists/{todoListId}/items/{id}
    [HttpGet("{id}")]
    public async Task<ActionResult<TodoItemDto>> GetTodoItem(int todoListId, int id)
    {
        var item = await _context.TodoItems
            .FirstOrDefaultAsync(i => i.Id == id && i.TodoListId == todoListId);

        if (item == null)
            return NotFound(new { message = $"TodoItem with id {id} not found in list {todoListId}" });

        return Ok(MapToDto(item));
    }

    // POST: api/todolists/{todoListId}/items
    [HttpPost]
    public async Task<ActionResult<TodoItemDto>> CreateTodoItem(int todoListId, CreateTodoItem dto)
    {
        var todoList = await _context.TodoLists.FindAsync(todoListId);
        if (todoList == null)
            return NotFound(new { message = $"TodoList with id {todoListId} not found" });

        var item = new TodoItem
        {
            Title = dto.Title,
            Description = dto.Description,
            TodoListId = todoListId,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        _context.TodoItems.Add(item);
        await _context.SaveChangesAsync();

        return CreatedAtAction(
            nameof(GetTodoItem),
            new { todoListId, id = item.Id },
            MapToDto(item)
        );
    }

    // PUT: api/todolists/{todoListId}/items/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> UpdateTodoItem(int todoListId, int id, UpdateTodoItem dto)
    {
        var item = await _context.TodoItems
            .FirstOrDefaultAsync(i => i.Id == id && i.TodoListId == todoListId);

        if (item == null)
            return NotFound(new { message = $"TodoItem with id {id} not found in list {todoListId}" });

        if (dto.Title != null) item.Title = dto.Title;
        if (dto.Description != null) item.Description = dto.Description;
        item.IsCompleted = dto.IsCompleted;
        item.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync();

        return NoContent();
    }

    // DELETE: api/todolists/{todoListId}/items/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> DeleteTodoItem(int todoListId, int id)
    {
        var item = await _context.TodoItems
            .FirstOrDefaultAsync(i => i.Id == id && i.TodoListId == todoListId);

        if (item == null)
            return NotFound(new { message = $"TodoItem with id {id} not found in list {todoListId}" });

        _context.TodoItems.Remove(item);
        await _context.SaveChangesAsync();

        return NoContent();
    }

    private static TodoItemDto MapToDto(TodoItem item) => new(
        item.Id,
        item.Description,
        item.IsCompleted,
        item.CreatedAt,
        item.UpdatedAt,
        item.TodoListId
    );
}
