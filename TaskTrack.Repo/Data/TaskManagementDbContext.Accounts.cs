using Microsoft.EntityFrameworkCore;
using TaskTrack.Repo.Models;

namespace TaskTrack.Repo.Data;

public partial class TaskManagementDbContext
{
    public DbSet<SystemAccount> Accounts => Set<SystemAccount>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<SystemAccount>(e =>
        {
            e.ToTable("SystemAccount");
            e.HasKey(x => x.AccountId);
            e.Property(x => x.AccountId).HasColumnName("AccountID");
            e.Property(x => x.FullName).HasMaxLength(100);
            e.Property(x => x.Email).HasMaxLength(254);
            e.HasIndex(x => x.Email).IsUnique();
        });
        modelBuilder.Entity<RefreshToken>(e =>
        {
            e.ToTable("RefreshToken");
            e.HasKey(x => x.RefreshTokenId);
            e.Property(x => x.RefreshTokenId).HasColumnName("RefreshTokenID");
            e.Property(x => x.AccountId).HasColumnName("AccountID");
            e.Property(x => x.FamilyId).HasColumnName("FamilyID");
            e.Property(x => x.ReplacedById).HasColumnName("ReplacedByID");
            e.Property(x => x.TokenHash).HasMaxLength(64);
            e.HasIndex(x => x.TokenHash).IsUnique();
            e.HasOne<SystemAccount>().WithMany().HasForeignKey(x => x.AccountId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<TaskItem>(e =>
        {
            e.Property(x => x.CreatedById).HasColumnName("CreatedByID");
            e.Property(x => x.UpdatedById).HasColumnName("UpdatedByID");
            e.HasOne<SystemAccount>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.Restrict);
            e.HasOne<SystemAccount>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.SetNull);
        });
        modelBuilder.Entity<Project>(e =>
        {
            e.Property(x => x.CreatedById).HasColumnName("CreatedByID");
            e.Property(x => x.UpdatedById).HasColumnName("UpdatedByID");
            e.HasOne<SystemAccount>().WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            e.HasOne<SystemAccount>().WithMany().HasForeignKey(x => x.UpdatedById).OnDelete(DeleteBehavior.SetNull);
        });
    }
}
