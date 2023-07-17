using Microsoft.AspNetCore.Identity;

namespace Backend.Models
{
    public class TodoUser : IdentityUser
    {
        public ICollection<TodoItem> TodoItems { get; set; } = null!;
    }
}