using Microsoft.EntityFrameworkCore;
using TaskTrack.Repo.Models;

namespace TaskTrack.Repo.Data;

// Database-first mapping of the provided PostgreSQL schema. The generated Task CLR type
// is named TaskItem to avoid ambiguity with System.Threading.Tasks.Task.
public partial class TaskManagementDbContext(DbContextOptions<TaskManagementDbContext> options) : DbContext(options)
{
    public virtual DbSet<Department> Departments => Set<Department>();
    public virtual DbSet<Project> Projects => Set<Project>();
    public virtual DbSet<TaskItem> Tasks => Set<TaskItem>();
    public virtual DbSet<Tag> Tags => Set<Tag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Department>(entity =>
        {
            entity.ToTable("Department");
            entity.HasKey(e => e.DepartmentId).HasName("Department_pkey");
            entity.Property(e => e.DepartmentId).HasColumnName("DepartmentID");
            entity.Property(e => e.DepartmentName).HasMaxLength(100).HasColumnName("DepartmentName");
            entity.Property(e => e.DepartmentDescription).HasMaxLength(300).HasColumnName("DepartmentDescription");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("IsActive");
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("Project");
            entity.HasKey(e => e.ProjectId).HasName("Project_pkey");
            entity.Property(e => e.ProjectId).HasColumnName("ProjectID");
            entity.Property(e => e.ProjectName).HasMaxLength(200).HasColumnName("ProjectName");
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.StartDate).HasColumnName("StartDate");
            entity.Property(e => e.EndDate).HasColumnName("EndDate");
            entity.Property(e => e.Status).HasDefaultValue((short)0).HasColumnName("Status");
            entity.Property(e => e.DepartmentId).HasColumnName("DepartmentID");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("IsActive");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("CreatedDate");
            entity.HasOne(e => e.Department).WithMany(e => e.Projects).HasForeignKey(e => e.DepartmentId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Project_Department");
        });

        modelBuilder.Entity<TaskItem>(entity =>
        {
            entity.ToTable("Task");
            entity.HasKey(e => e.TaskId).HasName("Task_pkey");
            entity.Property(e => e.TaskId).HasColumnName("TaskID");
            entity.Property(e => e.Title).HasMaxLength(300).HasColumnName("Title");
            entity.Property(e => e.Description).HasColumnName("Description");
            entity.Property(e => e.Status).HasDefaultValue((short)0).HasColumnName("Status");
            entity.Property(e => e.Priority).HasDefaultValue((short)1).HasColumnName("Priority");
            entity.Property(e => e.DueDate).HasColumnName("DueDate");
            entity.Property(e => e.ProjectId).HasColumnName("ProjectID");
            entity.Property(e => e.IsActive).HasDefaultValue(true).HasColumnName("IsActive");
            entity.Property(e => e.CreatedDate).HasDefaultValueSql("CURRENT_TIMESTAMP").HasColumnType("timestamp without time zone").HasColumnName("CreatedDate");
            entity.Property(e => e.ModifiedDate).HasColumnType("timestamp without time zone").HasColumnName("ModifiedDate");
            entity.HasOne(e => e.Project).WithMany(e => e.Tasks).HasForeignKey(e => e.ProjectId).OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_Task_Project");
        });

        modelBuilder.Entity<Tag>(entity =>
        {
            entity.ToTable("Tag");
            entity.HasKey(e => e.TagId).HasName("Tag_pkey");
            entity.HasIndex(e => e.TagName).IsUnique().HasDatabaseName("Tag_TagName_key");
            entity.Property(e => e.TagId).HasColumnName("TagID");
            entity.Property(e => e.TagName).HasMaxLength(50).HasColumnName("TagName");
            entity.Property(e => e.Color).HasMaxLength(7).HasColumnName("Color");
            entity.HasMany(e => e.Tasks).WithMany(e => e.Tags).UsingEntity<Dictionary<string, object>>(
                "TaskTag",
                right => right.HasOne<TaskItem>().WithMany().HasForeignKey("TaskID").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_TaskTag_Task"),
                left => left.HasOne<Tag>().WithMany().HasForeignKey("TagID").OnDelete(DeleteBehavior.Restrict).HasConstraintName("FK_TaskTag_Tag"),
                join =>
                {
                    join.ToTable("TaskTag");
                    join.HasKey("TaskID", "TagID").HasName("PK_TaskTag");
                });
        });

        OnModelCreatingPartial(modelBuilder);
    }

    partial void OnModelCreatingPartial(ModelBuilder modelBuilder);
}
