using Backend.Models;
using Backend.DTO;
using System.Security.Claims;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Backend.Lib;

namespace Backend.Controllers;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class TodoItemsController : ControllerBase
{
    private readonly TodoContext context;
    private readonly UserManager<TodoUser> _userManager;
    private readonly Token _token;

    public TodoItemsController(
        TodoContext context,
        UserManager<TodoUser> userManager,
        Token token
    )
    {
        this.context = context;
        _userManager = userManager;
        _token = token;
    }

    [HttpGet("todos")]
    public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodoList()
    {
        var accessToken = Request.Headers["Authorization"]
            .ToString()
            .Replace("Bearer ", string.Empty);
        if (accessToken == null || accessToken.Length == 0)
            return BadRequest("Invalid todo fetch");

        var token = _token.GenratePrincipalFromToken(accessToken!);

        var todos = await _userManager.Users
            .Where(x => x.Id == token.FindFirst("Id")!.Value)
            .SelectMany(u => u.TodoItems!)
            .Select(
                t =>
                    new TodoItemDto
                    {
                        Id = t.TodoItemId.ToString(),
                        Todo = t.Todo,
                        IsComplete = t.IsComplete
                    }
            )
            .ToListAsync();

        if (todos == null)
            return BadRequest("no data");

        return Ok(todos);
    }

    [HttpPost("todos")]
    public async Task<ActionResult<long>> AddTodo(TodoItemDto todo)
    {
        var accessToken = Request.Headers["Authorization"]
            .ToString()
            .Replace("Bearer ", string.Empty);
        if (accessToken == null || accessToken.Length == 0)
            return BadRequest("Invalid todo fetch");

        var token = _token.GenratePrincipalFromToken(accessToken!);

        var user = await _userManager.Users.FirstOrDefaultAsync(
            x => x.Id == token.FindFirst("Id")!.Value
        );

        if (user == null)
            return BadRequest("not existing user");

        var item = new TodoItem()
        {
            Todo = todo.Todo,
            IsComplete = todo.IsComplete,
            TodoUserId = user.Id,
            User = user
        };

        context.Todos.Add(item);
        await context.SaveChangesAsync();

        return item.TodoItemId;
    }

    [HttpDelete("todos/{id}")]
    public async Task<ActionResult<long>> DeleteTodo(long id)
    {
        var todo = await context.Todos.FindAsync(id);

        if (todo != null)
        {
            context.Todos.Remove(todo);
            await context.SaveChangesAsync();
        }

        return todo!.TodoItemId;
    }
}
