using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Services;

namespace TaskTrack.API.Controllers;

[ApiController, Route("api/auth")]
public sealed class AuthController(AuthService service) : ControllerBase
{
    [AllowAnonymous, HttpPost("register")]
    public async Task<IActionResult> Register(RegisterRequest request, CancellationToken ct) => StatusCode(201, await service.RegisterAsync(request, ct));
    [AllowAnonymous, HttpPost("login")]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken ct) => Ok(await service.LoginAsync(request, ct));
    [AllowAnonymous, HttpPost("refresh")]
    public async Task<IActionResult> Refresh(RefreshRequest request, CancellationToken ct) => Ok(await service.RefreshAsync(request.RefreshToken, ct));
    [AllowAnonymous, HttpPost("logout")]
    public async Task<IActionResult> Logout(RefreshRequest request, CancellationToken ct) { await service.LogoutAsync(request.RefreshToken, ct); return NoContent(); }
    [Authorize, HttpGet("me")]
    public async Task<IActionResult> Me(CancellationToken ct) => Ok(await service.MeAsync(ct));
    [Authorize, HttpPut("profile")]
    public async Task<IActionResult> Profile(ProfileRequest request, CancellationToken ct) => Ok(await service.ProfileAsync(request, ct));
    [Authorize, HttpPut("password")]
    public async Task<IActionResult> Password(PasswordRequest request, CancellationToken ct) { await service.PasswordAsync(request, ct); return NoContent(); }
}
