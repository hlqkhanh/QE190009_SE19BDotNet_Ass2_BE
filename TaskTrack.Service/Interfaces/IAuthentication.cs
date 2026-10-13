using TaskTrack.Repo.Models;

namespace TaskTrack.Service.Interfaces;

public interface ICurrentAccount { int AccountId { get; } int SessionVersion { get; } }
public interface ITokenIssuer { (string Token, DateTime ExpiresAt) Issue(SystemAccount account, DateTime sessionExpiresAt); }
