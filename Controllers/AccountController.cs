using Backend.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq.Expressions;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Backend.DTO;
using Microsoft.AspNetCore.Authorization;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AccountController : Controller
{
    private readonly TodoContext context;
    private readonly ILogger<TodoItemsController> _logger;
    private readonly IConfiguration _configuration;
    private readonly UserManager<TodoUser> _userManager;

    private readonly SignInManager<TodoUser> _signInManager;

    public AccountController(
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
                var result = await _userManager.CreateAsync(newUser, input.Password!);
                if (result.Succeeded)
                {
                    _logger.LogInformation(
                        "User {userName} ({email}) has been created.",
                        newUser.UserName,
                        newUser.Email
                    );
                    return StatusCode(201, $"User '{newUser.UserName}' has been created.");
                }
                else
                    throw new Exception(
                        string.Format(
                            "Error: {0}",
                            string.Join(" ", result.Errors.Select(e => e.Description))
                        )
                    );
            }
            else
            {
                var details = new ValidationProblemDetails(ModelState);
                details.Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1";
                details.Status = StatusCodes.Status400BadRequest;
                return new BadRequestObjectResult(details);
            }
        }
        catch (Exception e)
        {
            var exceptionDetails = new ProblemDetails();
            exceptionDetails.Detail = e.Message;
            exceptionDetails.Status = StatusCodes.Status500InternalServerError;
            exceptionDetails.Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1";
            return StatusCode(StatusCodes.Status500InternalServerError, exceptionDetails);
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
                if (user == null || !await _userManager.CheckPasswordAsync(user, input.Password!))
                    throw new Exception("invalid login attempt");
                else
                {
                    var accessToken = GenerateAccessToken(user);
                    var refreshToken = GenerateRefreshToken();

                    user.RefreshToken = refreshToken;
                    user.RefreshTokenExpiryTime = DateTime.Now.AddSeconds(20);

                    await _userManager.UpdateAsync(user);

                    var options = new CookieOptions()
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.None
                    };

                    Response.Cookies.Append("refresh-token", refreshToken, options);

                    return StatusCode(
                        StatusCodes.Status200OK,
                        new { accessToken, user = user.Id }
                    );
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
            var exceptionDetails = new ProblemDetails()
            {
                Detail = e.Message,
                Status = StatusCodes.Status401Unauthorized
            };
            return StatusCode(StatusCodes.Status401Unauthorized, exceptionDetails);
        }
    }

    [HttpPost]
    public async Task<IActionResult> Refresh()
    {
        var refreshToken = "";
        if (
            !Request.Headers.TryGetValue("user", out var userName)
            && !Request.Cookies.TryGetValue("refresh-token", out refreshToken)
        )
            BadRequest("refetsh error");

        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == userName);

        if (
            user == null
            || user.RefreshToken != refreshToken
            || user.RefreshTokenExpiryTime <= DateTime.Now
        )
            BadRequest("invalid refresh request");

        var newAccessToken = GenerateAccessToken(user!);
        var newRefreshToken = GenerateRefreshToken();

        user!.RefreshToken = newRefreshToken;

        await _userManager.UpdateAsync(user);

        var options = new CookieOptions() { HttpOnly = true, SameSite = SameSiteMode.None };

        Response.Cookies.Append("refresh-token", newAccessToken, options);

        Response.Headers.Add("Access-Control-Allow-Credentials", "true");

        return Ok(new { accessToken = newAccessToken, userName = user.UserName });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Revoke()
    {
        if (!Request.Headers.TryGetValue("user", out var userName))
            BadRequest();

        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == userName);

        if (user == null)
            BadRequest();

        user!.RefreshToken = null;
        await _userManager.UpdateAsync(user);

        return NoContent();
    }

    private SigningCredentials CreateSigningCredentials()
    {
        return new SigningCredentials(
            new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(_configuration["JWT:SigningKey"]!)
            ),
            SecurityAlgorithms.HmacSha256
        );
    }

    private List<Claim> CreateClaims(IdentityUser user)
    {
        List<Claim> claims = new() { new Claim(ClaimTypes.Name, user.UserName!) };

        return claims;
    }

    private string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var numGen = RandomNumberGenerator.Create();
        numGen.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    private string GenerateAccessToken(IdentityUser user)
    {
        var signingCredentials = CreateSigningCredentials();

        var claims = CreateClaims(user);

        var jwtObject = new JwtSecurityToken(
            issuer: _configuration["JWT:Issuer"],
            audience: _configuration["JWT:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddSeconds(30),
            signingCredentials: signingCredentials
        );

        var accessToken = new JwtSecurityTokenHandler().WriteToken(jwtObject);
        return accessToken;
    }
}
