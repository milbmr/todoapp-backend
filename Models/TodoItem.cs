namespace Backend.Models;

public class TodoItem
{
    public long TodoItemId { get; set; }
    public string? Todo { get; set; }
    public bool IsComplete { get; set; }
    public long TodoUserId { get; set; }
    public TodoUser User { get; set; } = null!;
}