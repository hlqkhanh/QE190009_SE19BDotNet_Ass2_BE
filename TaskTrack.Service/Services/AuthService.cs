using System.Security.Cryptography;
using System.Text;
using TaskTrack.Repo.Interfaces;
using TaskTrack.Repo.Models;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Exceptions;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.Service.Services;

public sealed class AuthService(IAccountRepository accounts, ITokenIssuer issuer, ICurrentAccount current)
{
    public static AccountDto ToDto(SystemAccount a) => new(a.AccountId, a.FullName, a.Email, a.Role, a.CreatedDate);
    public Task<AccountDto> RegisterAsync(RegisterRequest request, CancellationToken ct) => accounts.TransactionAsync(async () =>
    {
        var email = request.Email.Trim().ToLowerInvariant();
        if (await accounts.FindEmailAsync(email, ct) is not null) throw new ConflictException("Email is already registered.");
        var account = new SystemAccount { FullName = request.FullName.Trim(), Email = email, PasswordHash = PasswordHasher.Hash(request.Password), Role = 0 };
        accounts.Add(account);
        await accounts.SaveAsync(ct);
        return ToDto(account);
    }, ct);

    public Task<SessionDto> LoginAsync(LoginRequest request, CancellationToken ct) => accounts.TransactionAsync(async () =>
    {
        var account = await accounts.FindEmailAsync(request.Email.Trim().ToLowerInvariant(), ct);
        if (account is null || !PasswordHasher.Verify(request.Password, account.PasswordHash)) throw new AuthenticationException("Invalid email or password.");
        var expiry = DateTime.UtcNow.AddDays(7);
        // PostgreSQL stores microseconds; return the same precision we persist.
        expiry = expiry.AddTicks(-(expiry.Ticks % 10));
        var session = CreateSession(account, Guid.NewGuid(), expiry);
        await accounts.SaveAsync(ct);
        return session;
    }, ct);

    public async Task<SessionDto> RefreshAsync(string raw, CancellationToken ct)
    {
        // Return an invalid result inside the transaction so replay revocation is committed.
        var session = await accounts.TransactionAsync<SessionDto?>(async () =>
        {
            var token = await accounts.FindTokenAsync(TokenHash(raw), ct);
            if (token is null) return null;
            if (token.RevokedDate is not null)
            {
                if (token.ReplacedById is not null) await accounts.RevokeAsync(token.AccountId, token.FamilyId, ct);
                return null;
            }
            if (token.ExpiresDate <= DateTime.UtcNow) return null;
            var account = await accounts.FindAsync(token.AccountId, ct);
            if (account is null) return null;
            var replacement = NewRefresh(account.AccountId, token.FamilyId, token.ExpiresDate);
            token.RevokedDate = DateTime.UtcNow;
            token.ReplacedById = replacement.Entity.RefreshTokenId;
            accounts.Add(replacement.Entity);
            var access = issuer.Issue(account, token.ExpiresDate);
            await accounts.SaveAsync(ct);
            return new SessionDto(access.Token, replacement.Raw, access.ExpiresAt, token.ExpiresDate, ToDto(account));
        }, ct);
        return session ?? throw new AuthenticationException("Refresh token is invalid or expired. Please log in again.");
    }

    public async Task LogoutAsync(string raw, CancellationToken ct)
    {
        var valid = await accounts.TransactionAsync(async () =>
        {
            var token = await accounts.FindTokenAsync(TokenHash(raw), ct);
            if (token is null || token.RevokedDate is not null || token.ExpiresDate <= DateTime.UtcNow) return false;
            var account = await accounts.FindAsync(token.AccountId, ct);
            if (account is null) return false;
            account.SessionVersion++;
            await accounts.RevokeAsync(account.AccountId, null, ct);
            await accounts.SaveAsync(ct);
            return true;
        }, ct);
        if (!valid) throw new AuthenticationException("Session is invalid.");
    }

    public async Task<AccountDto> MeAsync(CancellationToken ct) => ToDto(await RequiredAccountAsync(ct));
    public Task<AccountDto> ProfileAsync(ProfileRequest request, CancellationToken ct) => accounts.TransactionAsync(async () =>
    {
        var account = await RequiredAccountAsync(ct);
        account.FullName = request.FullName.Trim();
        await accounts.SaveAsync(ct);
        return ToDto(account);
    }, ct);
    public async Task PasswordAsync(PasswordRequest request, CancellationToken ct) => await accounts.TransactionAsync(async () =>
    {
        var account = await RequiredAccountAsync(ct);
        if (!PasswordHasher.Verify(request.CurrentPassword, account.PasswordHash)) throw new ServiceValidationException("currentPassword", "Current password is incorrect.");
        account.PasswordHash = PasswordHasher.Hash(request.NewPassword);
        account.SessionVersion++;
        await accounts.RevokeAsync(account.AccountId, null, ct);
        await accounts.SaveAsync(ct);
        return true;
    }, ct);

    private async Task<SystemAccount> RequiredAccountAsync(CancellationToken ct)
    {
        var account = await accounts.FindAsync(current.AccountId, ct);
        if (account is null || account.SessionVersion != current.SessionVersion) throw new AuthenticationException("Session is no longer valid.");
        return account;
    }
    private SessionDto CreateSession(SystemAccount account, Guid family, DateTime expiry)
    {
        var refresh = NewRefresh(account.AccountId, family, expiry);
        accounts.Add(refresh.Entity);
        var access = issuer.Issue(account, expiry);
        return new(access.Token, refresh.Raw, access.ExpiresAt, expiry, ToDto(account));
    }
    private static (RefreshToken Entity, string Raw) NewRefresh(int accountId, Guid family, DateTime expiry)
    {
        var raw = Convert.ToBase64String(RandomNumberGenerator.GetBytes(48));
        return (new RefreshToken { AccountId = accountId, FamilyId = family, TokenHash = TokenHash(raw), CreatedDate = DateTime.UtcNow, ExpiresDate = expiry }, raw);
    }
    private static string TokenHash(string raw) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(raw)));
}
