using System.ComponentModel.DataAnnotations;

namespace TaskTrack.Service.DTOs;

public sealed record AccountDto(int AccountId, string FullName, string Email, short Role, DateTime CreatedDate);
public sealed record SessionDto(string AccessToken, string RefreshToken, DateTime ExpiresAt, DateTime RefreshExpiresAt, AccountDto Account);
public sealed class RegisterRequest
{
    [Required, NotWhiteSpace, StringLength(100)] public string FullName { get; set; } = "";
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string Password { get; set; } = "";
}
public sealed class LoginRequest
{
    [Required, EmailAddress, StringLength(254)] public string Email { get; set; } = "";
    [Required, StringLength(128)] public string Password { get; set; } = "";
}
public sealed class RefreshRequest
{
    [Required, StringLength(256)] public string RefreshToken { get; set; } = "";
}
public sealed class ProfileRequest
{
    [Required, NotWhiteSpace, StringLength(100)] public string FullName { get; set; } = "";
}
public sealed class PasswordRequest
{
    [Required, StringLength(128)] public string CurrentPassword { get; set; } = "";
    [Required, StringLength(128, MinimumLength = 8)] public string NewPassword { get; set; } = "";
}
public sealed class AccountUpdateRequest : IValidatableObject
{
    [NotWhiteSpace, StringLength(100)] public string? FullName { get; set; }
    [Range(0, 1)] public short? Role { get; set; }
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (FullName is null && Role is null) yield return new ValidationResult("Provide a name or role.");
    }
}
