using TaskTrack.Repo.Models;

namespace TaskTrack.Repo.Interfaces;

public interface IAccountRepository
{
    Task<T> TransactionAsync<T>(Func<Task<T>> action, CancellationToken ct);
    Task<SystemAccount?> FindAsync(int id, CancellationToken ct);
    Task<SystemAccount?> FindEmailAsync(string email, CancellationToken ct);
    Task<IReadOnlyList<SystemAccount>> ListAsync(CancellationToken ct);
    Task<int> AdminCountAsync(CancellationToken ct);
    Task<bool> HasCreatedTasksAsync(int id, CancellationToken ct);
    Task<RefreshToken?> FindTokenAsync(string hash, CancellationToken ct);
    Task RevokeAsync(int accountId, Guid? familyId, CancellationToken ct);
    void Add(SystemAccount account);
    void Add(RefreshToken token);
    void Remove(SystemAccount account);
    Task SaveAsync(CancellationToken ct);
}
