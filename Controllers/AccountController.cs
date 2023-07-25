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
using Microsoft.IdentityModel.Logging;
using Backend.Lib;

namespace Backend.Controllers;

[ApiController]
[Route("api/[controller]/[action]")]
public class AccountController : Controller
{
    private readonly TodoContext context;
    private readonly UserManager<TodoUser> _userManager;
    private readonly Token _token;

    public AccountController(
        TodoContext context,
        UserManager<TodoUser> userManager,
        Token token
    )
    {
        this.context = context;
        _userManager = userManager;
        _token = token;
    }

    [HttpPost]
    public async Task<ActionResult> Register(RegisterDTO input)
    {
        try
        {
            if (ModelState.IsValid)
            {
                var newUser = new TodoUser { UserName = input.UserName, Email = input.Email };
                var result = await _userManager.CreateAsync(newUser, input.Password!);
                if (result.Succeeded)
                {
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
                    var accessToken = _token.GenerateAccessToken(user);
                    var refreshToken = _token.GenerateRefreshToken();

                    user.RefreshToken = refreshToken;
                    user.RefreshTokenExpiryTime = DateTime.Now.AddSeconds(20);

                    await context.SaveChangesAsync();

                    var options = new CookieOptions()
                    {
                        HttpOnly = true,
                        SameSite = SameSiteMode.None
                    };

                    Response.Cookies.Append("refresh-token", refreshToken, options);

                    return StatusCode(StatusCodes.Status200OK, new { accessToken, user = user.Id });
                }
            }
            else
            {
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
        var httpOnlyCookie = Request.Cookies["refresh-token"];
        if (!Request.Headers.TryGetValue("user", out var userName) && httpOnlyCookie == "")
            return BadRequest("refetsh error");

        var user = await _userManager.Users.FirstOrDefaultAsync(x => x.UserName == userName);

        if (
            user == null
            || user.RefreshToken != httpOnlyCookie
            || user.RefreshTokenExpiryTime <= DateTime.Now
        )
            return BadRequest("invalid refresh request");

        var newAccessToken = _token.GenerateAccessToken(user!);
        var newRefreshToken = _token.GenerateRefreshToken();

        user!.RefreshToken = newRefreshToken;

        await context.SaveChangesAsync();

        var options = new CookieOptions() { HttpOnly = true, SameSite = SameSiteMode.None };

        Response.Cookies.Append("refresh-token", newRefreshToken, options);

        Response.Headers.Add("Access-Control-Allow-Credentials", "true");

        return Ok(new { accessToken = newAccessToken, userName = user.UserName });
    }

    [HttpPost, Authorize]
    public async Task<IActionResult> Revoke()
    {
        var accessToken = Request.Headers["Authorization"]
            .ToString()
            .Replace("Bearer ", string.Empty);
        if (accessToken == null || accessToken.Length == 0)
            return BadRequest("Invalid todo fetch");

        var token = _token.GenratePrincipalFromToken(accessToken!);

        //var userId = User.FindFirst("Id")!.Value;

        var user = await _userManager.Users.FirstOrDefaultAsync(
            x => x.UserName == token.FindFirst("Id")!.Value
        );

        if (user == null)
            return BadRequest();

        user!.RefreshToken = null;
        await _userManager.UpdateAsync(user);

        return NoContent();
    }
}
