using Backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Backend.DTO;

namespace Backend.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class TodoItemsController : ControllerBase
    {
        private readonly TodoContext context;
        private readonly ILogger<TodoItemsController> _logger;
        private readonly IConfiguration _configuration;
        private readonly UserManager<TodoUser> _userManager;

        private readonly SignInManager<TodoUser> _signInManager;

        public TodoItemsController(
            ILogger<TodoItemsController> logger,
            TodoContext context,
            IConfiguration configuration,
            UserManager<TodoUser> userManager,
            SignInManager<TodoUser> signInManager
        )
        {
            this.context = context;
            _configuration = configuration;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [HttpGet("todos")]
        public async Task<ActionResult<IEnumerable<TodoItem>>> GetTodoList()
        {
            if (!Request.Headers.TryGetValue("user", out var userName))
                BadRequest("Invalid todo fetch");

            var user = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == userName);
            var todos = await context.Todos.Where(t => t.TodoUserId == user!.Id).ToListAsync();

            if (user != null || todos != null)
                BadRequest("no data");

            return Ok(new {GetTodoList = user!.TodoItems});
        }

        [HttpPost("todos")]
        public async Task<ActionResult<long>> AddTodo(TodoItemDto todo)
        {
            var user = await _userManager.Users.FirstOrDefaultAsync(x => x.Id == todo.Id);
            var item = new TodoItem()
            {
                Todo = todo.Todo,
                IsComplete = todo.IsComplete,
                TodoUserId = todo.Id,
                User = user!
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
}
