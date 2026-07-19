using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using Bashta.API.Requests;
using Bashta.Core.Entities;
using Bashta.Infrastructure.Data;

using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Bashta.API.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly BashtaDbContext _dbContext;
    private readonly IPasswordHasher<User> _passwordHasher;
    private readonly IConfiguration _configuration;
    private readonly IWebHostEnvironment _environment;

    public AuthController(
        BashtaDbContext dbContext,
        IPasswordHasher<User> passwordHasher,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        _dbContext = dbContext;
        _passwordHasher = passwordHasher;
        _configuration = configuration;
        _environment = environment;
    }

    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(
        [FromBody] LoginRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Login) ||
            string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Korisničko ime/email i lozinka su obavezni.");
        }

        var login = request.Login.Trim().ToLower();

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u =>
                u.IsActive &&
                (
                    u.Email.ToLower() == login ||
                    u.Username.ToLower() == login
                ));

        if (user is null)
        {
            return Unauthorized("Pogrešno korisničko ime/email ili lozinka.");
        }

        var verificationResult =
            _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                request.Password);

        if (verificationResult == PasswordVerificationResult.Failed)
        {
            return Unauthorized("Pogrešno korisničko ime/email ili lozinka.");
        }

        // Ako framework preporuči novi format hasha, automatski refresh
        if (verificationResult ==
            PasswordVerificationResult.SuccessRehashNeeded)
        {
            user.PasswordHash =
                _passwordHasher.HashPassword(user, request.Password);

            await _dbContext.SaveChangesAsync();
        }

        var expiresAt = DateTime.UtcNow.AddHours(8);

        var token = GenerateToken(user, expiresAt);

        return Ok(new LoginResponse
        {
            Token = token,
            UserId = user.Id,
            Username = user.Username,
            Email = user.Email,
            ExpiresAt = expiresAt
        });
    }

    private string GenerateToken(User user, DateTime expiresAt)
    {
        var key = _configuration["Jwt:Key"];

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new InvalidOperationException(
                "JWT ključ nije konfigurisan.");
        }

        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var claims = new List<Claim>
        {
            new(
                ClaimTypes.NameIdentifier,
                user.Id.ToString()),

            new(
                ClaimTypes.Name,
                user.Username),

            new(
                ClaimTypes.Email,
                user.Email)
        };

        var securityKey =
            new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(key));

        var credentials =
            new SigningCredentials(
                securityKey,
                SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: expiresAt,
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler()
            .WriteToken(token);
    }

    // privremeno ya hased password
    [HttpPost("dev-set-password/{userId:int}")]
    public async Task<IActionResult> SetDevelopmentPassword(
        int userId,
        [FromBody] SetPasswordRequest request)
    {
        if (!_environment.IsDevelopment())
        {
            return NotFound();
        }

        if (string.IsNullOrWhiteSpace(request.Password))
        {
            return BadRequest("Lozinka je obavezna.");
        }

        var user = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userId);

        if (user is null)
        {
            return NotFound($"Korisnik {userId} nije pronađen.");
        }

        user.PasswordHash =
            _passwordHasher.HashPassword(
                user,
                request.Password);

        await _dbContext.SaveChangesAsync();

        return Ok(new
        {
            message = "Password je uspješno postavljen.",
            userId = user.Id,
            username = user.Username
        });
    }
}