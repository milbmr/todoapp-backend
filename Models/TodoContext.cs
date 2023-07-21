using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;

namespace Backend.Models;

public class TodoContext : IdentityDbContext<TodoUser>
{
    public TodoContext(DbContextOptions<TodoContext> options)
        : base(options) { }

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);
        builder.Entity<TodoItem>(table =>
        {
            table.HasKey(x => x.TodoItemId);
            table
                .HasOne(x => x.User)
                .WithMany(x => x.TodoItems)
                .HasForeignKey(x => x.TodoUserId)
                .HasPrincipalKey(x => x.Id)
                .OnDelete(DeleteBehavior.Cascade);
        });
    }

    public DbSet<TodoItem> Todos { get; set; } = null!;
}
