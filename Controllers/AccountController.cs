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
using Backend.DTO;

namespace Backend.Controllers
{
    [ApiController]
    [Route("[controller]/[action]")]
    public class AccountController : Controller
    {
        private readonly TodoContext context;
        private readonly ILogger<TodoItemsController> _logger;
        private readonly IConfiguration _configuration;
        private readonly UserManager<TodoUser> _userManager;

        private readonly SignInManager<TodoUser> _signInManager;

        public AccountController(ILogger<TodoItemsController> logger, TodoContext context,
            IConfiguration configuration,
            UserManager<TodoUser> userManager,
            SignInManager<TodoUser> signInManager)
        {
            this.context = context;
            _configuration = configuration;
            _userManager = userManager;
            _signInManager = signInManager;
            _logger = logger;
        }

        [HttpPost]
        public async Task<ActionResult> Register(RegisterDTO input)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var newUser = new TodoUser();
                    newUser.UserName = input.UserName;
                    newUser.Email = input.Email;
                    var result = await _userManager.CreateAsync(
                        newUser, input.Password!);
                    if (result.Succeeded)
                    {
                        _logger.LogInformation(
                            "User {userName} ({email}) has been created.",
                            newUser.UserName, newUser.Email);
                        return StatusCode(201,
                            $"User '{newUser.UserName}' has been created.");
                    }
                    else
                        throw new Exception(
                            string.Format("Error: {0}",
                            string.Join(" ", result.Errors.Select(e => e.Description))));
                }
                else
                {
                    var details = new ValidationProblemDetails(ModelState);
                    details.Type =
                        "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                    details.Status = StatusCodes.Status400BadRequest;
                    return new BadRequestObjectResult(details);
                }
            }
            catch (Exception e)
            {
                var exceptionDetails = new ProblemDetails();
                exceptionDetails.Detail = e.Message;
                exceptionDetails.Status =
                    StatusCodes.Status500InternalServerError;
                exceptionDetails.Type =
                    "https://tools.ietf.org/html/rfc7231#section-6.6.1";
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    exceptionDetails);
            }
        }

        [HttpPost]
        public async Task<ActionResult> Login(LoginDTO input)
        {
            try
            {
                if (ModelState.IsValid)
                {
                    var user = await _userManager.FindByNameAsync(input.UserName!);
                    if (user == null ||
                    !await _userManager.CheckPasswordAsync(user, input.Password!))
                        throw new Exception("invalid login attempt");

                    else
                    {
                        var signingCredentials = CreateSigningCredentials();

                        var claims = CreateClaims(user);

                        var jwtObject = new JwtSecurityToken(
                            issuer: _configuration["JWT:Issuer"],
                            audience: _configuration["JWT:Audience"],
                            claims: claims,
                            expires: DateTime.Now.AddSeconds(300),
                            signingCredentials: signingCredentials
                        );

                        var jwtString = new JwtSecurityTokenHandler().WriteToken(jwtObject);

                        return StatusCode(StatusCodes.Status200OK, jwtString);
                        Response.Cookies.Append("x", jwtString);
                    }

                }
                else
                {
                    Console.WriteLine("in auth");
                    var details = new ValidationProblemDetails(ModelState);
                    details.Status = StatusCodes.Status400BadRequest;
                    return new BadRequestObjectResult(details);
                }
            }
            catch (Exception e)
            {
                var exceptionDetails = new ProblemDetails();
                exceptionDetails.Detail = e.Message;
                exceptionDetails.Status = StatusCodes.Status401Unauthorized;
                return StatusCode(
                    StatusCodes.Status401Unauthorized, exceptionDetails
                );
            }
        }

        private SigningCredentials CreateSigningCredentials()
        {
            Console.WriteLine("inside credentials");
            return new SigningCredentials(
                            new SymmetricSecurityKey(
                                System.Text.Encoding.UTF8.GetBytes(
                                    _configuration["JWT:SigningKey"]!
                                )
                            ),
                            SecurityAlgorithms.HmacSha256
                        );
        }

        private List<Claim> CreateClaims(IdentityUser user)
        {
            try
            {
                Console.WriteLine("in claims");
                List<Claim> claims = new List<Claim>() {
                    new Claim(ClaimTypes.Name, user.UserName!)
                };

                return claims;
            }
            catch (Exception e)
            {
                Console.WriteLine("problem with claims", e);
                throw;
            };

        }
    }
}