namespace TaskTrack.Repo.Models;

public sealed class SystemAccount
{
    public int AccountId { get; set; }
    public string FullName { get; set; } = "";
    public string Email { get; set; } = "";
    public string PasswordHash { get; set; } = "";
    public short Role { get; set; }
    public DateTime CreatedDate { get; set; } = DateTime.UtcNow;
    public int SessionVersion { get; set; }
}

public sealed class RefreshToken
{
    public Guid RefreshTokenId { get; set; } = Guid.NewGuid();
    public int AccountId { get; set; }
    public string TokenHash { get; set; } = "";
    public Guid FamilyId { get; set; }
    public DateTime CreatedDate { get; set; }
    public DateTime ExpiresDate { get; set; }
    public DateTime? RevokedDate { get; set; }
    public Guid? ReplacedById { get; set; }
}
