namespace TodoApi.Dtos;

public class UpdateTodoList
{
    public required string Name { get; set; }
    public List<UpdateTodoItem>? Items { get; set; }
}
