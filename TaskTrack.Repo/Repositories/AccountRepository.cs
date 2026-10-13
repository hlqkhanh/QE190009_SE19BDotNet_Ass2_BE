using Microsoft.EntityFrameworkCore;
using TaskTrack.Repo.Data;
using TaskTrack.Repo.Interfaces;
using TaskTrack.Repo.Models;

namespace TaskTrack.Repo.Repositories;

public sealed class AccountRepository(TaskManagementDbContext db) : IAccountRepository
{
    public async Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        // Shared by seed/auth/account writes: prevent rotation and last-admin races.
        await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(232002)", ct);
        db.ChangeTracker.Clear();
        var result = await action();
        await tx.CommitAsync(ct);
        return result;
    }
    public Task<SystemAccount?> FindAsync(int id, CancellationToken ct) => db.Accounts.FirstOrDefaultAsync(x => x.AccountId == id, ct);
    public Task<SystemAccount?> FindEmailAsync(string email, CancellationToken ct) => db.Accounts.FirstOrDefaultAsync(x => x.Email == email, ct);
    public async Task<IReadOnlyList<SystemAccount>> ListAsync(CancellationToken ct) => await db.Accounts.AsNoTracking().OrderBy(x => x.AccountId).ToListAsync(ct);
    public Task<int> AdminCountAsync(CancellationToken ct) => db.Accounts.CountAsync(x => x.Role == 1, ct);
    public Task<bool> HasCreatedTasksAsync(int id, CancellationToken ct) => db.Tasks.AnyAsync(x => x.CreatedById == id, ct);
    public Task<RefreshToken?> FindTokenAsync(string hash, CancellationToken ct) => db.RefreshTokens.FirstOrDefaultAsync(x => x.TokenHash == hash, ct);
    public async Task RevokeAsync(int accountId, Guid? familyId, CancellationToken ct)
    {
        var query = db.RefreshTokens.Where(x => x.AccountId == accountId && x.RevokedDate == null);
        if (familyId.HasValue) query = query.Where(x => x.FamilyId == familyId);
        await query.ExecuteUpdateAsync(s => s.SetProperty(x => x.RevokedDate, DateTime.UtcNow), ct);
    }
    public void Add(SystemAccount account) => db.Accounts.Add(account);
    public void Add(RefreshToken token) => db.RefreshTokens.Add(token);
    public void Remove(SystemAccount account) => db.Accounts.Remove(account);
    public Task SaveAsync(CancellationToken ct) => db.SaveChangesAsync(ct);
}
