using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using TaskTrack.Repo.Interfaces;
using TaskTrack.Repo.Models;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.API.Authentication;

public sealed class CurrentAccount(IHttpContextAccessor accessor) : ICurrentAccount
{
    public int SessionVersion => int.TryParse(accessor.HttpContext?.User.FindFirstValue("SessionVersion"), out var version) ? version : throw new TaskTrack.Service.Exceptions.AuthenticationException("Authentication required.");
    public int AccountId => int.TryParse(accessor.HttpContext?.User.FindFirstValue("AccountID"), out var id) ? id : throw new TaskTrack.Service.Exceptions.AuthenticationException("Authentication required.");
}

public sealed class JwtSettings(IConfiguration configuration)
{
    public string Secret => configuration["JWT_SECRET"] ?? configuration["Jwt:Secret"] ?? "";
    public string Issuer => configuration["Jwt:Issuer"] ?? "TaskTrack.Ass2";
    public string Audience => configuration["Jwt:Audience"] ?? "TaskTrack.Frontend";
    public void Validate()
    {
        if (Encoding.UTF8.GetByteCount(Secret) < 32) throw new InvalidOperationException("Set JWT_SECRET to a secret containing at least 32 UTF-8 bytes.");
    }
}

public sealed class JwtTokenIssuer(JwtSettings settings) : ITokenIssuer
{
    public (string Token, DateTime ExpiresAt) Issue(SystemAccount account, DateTime sessionExpiresAt)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddHours(24) < sessionExpiresAt ? now.AddHours(24) : sessionExpiresAt;
        var claims = new[] {
            new Claim("AccountID", account.AccountId.ToString()), new Claim("Email", account.Email),
            new Claim("Role", account.Role == 1 ? "Admin" : "Staff"), new Claim("FullName", account.FullName),
            new Claim("SessionVersion", account.SessionVersion.ToString()), new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };
        var jwt = new JwtSecurityToken(settings.Issuer, settings.Audience, claims, now, expires,
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)), SecurityAlgorithms.HmacSha256));
        return (new JwtSecurityTokenHandler().WriteToken(jwt), expires);
    }
}

public static class JwtConfiguration
{
    public static void AddTaskTrackAuthentication(this IServiceCollection services, IConfiguration configuration)
    {
        var settings = new JwtSettings(configuration);
        services.AddSingleton(settings);
        services.AddSingleton<ITokenIssuer, JwtTokenIssuer>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentAccount, CurrentAccount>();
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Secret)),
                ValidateIssuer = true, ValidIssuer = settings.Issuer, ValidateAudience = true, ValidAudience = settings.Audience,
                ValidateLifetime = true, RequireExpirationTime = true, ClockSkew = TimeSpan.Zero,
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256], NameClaimType = "FullName", RoleClaimType = "Role"
            };
            options.Events = new JwtBearerEvents
            {
                OnTokenValidated = async context =>
                {
                    var principal = context.Principal!;
                    if (!int.TryParse(principal.FindFirstValue("AccountID"), out var id) || !int.TryParse(principal.FindFirstValue("SessionVersion"), out var version)) { context.Fail("Invalid claims."); return; }
                    var repository = context.HttpContext.RequestServices.GetRequiredService<IAccountRepository>();
                    var account = await repository.FindAsync(id, context.HttpContext.RequestAborted);
                    if (account is null || account.SessionVersion != version || principal.FindFirstValue("Role") != (account.Role == 1 ? "Admin" : "Staff")) context.Fail("Session is no longer valid.");
                }
            };
        });
        services.AddAuthorization();
    }
}
