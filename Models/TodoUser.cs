using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Identity;

namespace Backend.Models
{
    public class TodoUser : IdentityUser
    {
        [ForeignKey("TodoItemId")]
        public virtual ICollection<TodoItem>? TodoItems { get; set; }
        public string? RefreshToken { get; set; }
        public DateTime RefreshTokenExpiryTime { get; set; }
    }
}