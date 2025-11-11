using Microsoft.AspNetCore.SignalR;

namespace TodoApi.Hubs;

public class NotificationsHub : Hub
{
    // Client calls this to join a group for a specific todo list
    public Task JoinTodoListGroup(long todoListId)
    {
        return Groups.AddToGroupAsync(Context.ConnectionId, GroupName(todoListId));
    }

    public Task LeaveTodoListGroup(long todoListId)
    {
        return Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupName(todoListId));
    }

    private static string GroupName(long todoListId) => $"todolist-{todoListId}";
}
