using System.ComponentModel;
using System.Text.Json.Serialization;

namespace Backend.Models;

public class TodoItem
{
    public long TodoItemId { get; set; }
    public string? Todo { get; set; }
    
    public bool IsComplete { get; set; }
    public virtual string? TodoUserId { get; set; }
    public virtual TodoUser User { get; set; } = null!;
}