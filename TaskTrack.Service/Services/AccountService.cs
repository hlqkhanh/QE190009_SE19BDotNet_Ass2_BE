using TaskTrack.Repo.Interfaces;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Exceptions;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.Service.Services;

public sealed class AccountService(IAccountRepository accounts, ICurrentAccount current)
{
    public async Task<IReadOnlyList<AccountDto>> ListAsync(CancellationToken ct) => (await accounts.ListAsync(ct)).Select(AuthService.ToDto).ToList();
    public async Task<AccountDto> GetAsync(int id, CancellationToken ct) => AuthService.ToDto(await accounts.FindAsync(id, ct) ?? throw new ResourceNotFoundException("Account was not found."));
    public async Task UpdateAsync(int id, AccountUpdateRequest request, CancellationToken ct) => await accounts.TransactionAsync(async () =>
    {
        await RequireCurrentAdminAsync(ct);
        var account = await accounts.FindAsync(id, ct) ?? throw new ResourceNotFoundException("Account was not found.");
        if (request.Role.HasValue && request.Role != account.Role)
        {
            if (account.Role == 1 && await accounts.AdminCountAsync(ct) <= 1) throw new ConflictException("Cannot demote the last Admin.");
            account.Role = request.Role.Value;
            account.SessionVersion++;
            await accounts.RevokeAsync(id, null, ct);
        }
        if (request.FullName is not null) account.FullName = request.FullName.Trim();
        await accounts.SaveAsync(ct);
        return true;
    }, ct);
    public async Task DeleteAsync(int id, CancellationToken ct) => await accounts.TransactionAsync(async () =>
    {
        await RequireCurrentAdminAsync(ct);
        var account = await accounts.FindAsync(id, ct) ?? throw new ResourceNotFoundException("Account was not found.");
        if (await accounts.HasCreatedTasksAsync(id, ct)) throw new ConflictException("Cannot delete an account that has created tasks, including archived tasks.");
        if (account.Role == 1 && await accounts.AdminCountAsync(ct) <= 1) throw new ConflictException("Cannot delete the last Admin.");
        accounts.Remove(account);
        await accounts.SaveAsync(ct);
        return true;
    }, ct);
    private async Task RequireCurrentAdminAsync(CancellationToken ct)
    {
        var caller = await accounts.FindAsync(current.AccountId, ct);
        if (caller is null || caller.Role != 1 || caller.SessionVersion != current.SessionVersion) throw new AuthenticationException("Admin session is no longer valid.");
    }
}
