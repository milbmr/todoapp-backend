using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Logging;
using Microsoft.IdentityModel.Tokens;

namespace Backend.Lib;

public class Token
{
    private readonly IConfiguration _configuration;

    public Token(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public SigningCredentials CreateSigningCredentials()
    {
        return new SigningCredentials(
            new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(_configuration["JWT:SigningKey"]!)
            ),
            SecurityAlgorithms.HmacSha256
        );
    }

    public List<Claim> CreateClaims(IdentityUser user)
    {
        List<Claim> claims =
            new() { new Claim(ClaimTypes.Name, user.UserName!), new Claim("Id", user.Id) };

        return claims;
    }

    public string GenerateRefreshToken()
    {
        var randomNumber = new byte[32];
        using var numGen = RandomNumberGenerator.Create();
        numGen.GetBytes(randomNumber);
        return Convert.ToBase64String(randomNumber);
    }

    public string GenerateAccessToken(IdentityUser user)
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

    public ClaimsPrincipal GenratePrincipalFromToken(string token)
    {
        IdentityModelEventSource.ShowPII = true;

        var tokenValidation = new TokenValidationParameters()
        {
            ValidateAudience = true,
            ValidateIssuer = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes(_configuration["JWT:SigningKey"]!)
            ),
            ValidateLifetime = true,
        };

        var tokenHandler = new JwtSecurityTokenHandler();
        var principal = tokenHandler.ValidateToken(
            token,
            tokenValidation,
            out SecurityToken securityToken
        );

        if (
            securityToken is not JwtSecurityToken jwtSecurityToken
            || !jwtSecurityToken.Header.Alg.Equals(
                SecurityAlgorithms.HmacSha256,
                StringComparison.InvariantCultureIgnoreCase
            )
        )
            throw new SecurityTokenException("Invalid token");

        return principal;
    }
}
