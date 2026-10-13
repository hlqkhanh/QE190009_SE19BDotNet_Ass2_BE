using Microsoft.EntityFrameworkCore;
using TaskTrack.Repo.Data;
using TaskTrack.Repo.Interfaces;
using TaskTrack.Repo.Models;

namespace TaskTrack.Repo.Repositories;

public sealed class DepartmentRepository(TaskManagementDbContext context) : IDepartmentRepository
{
    public async Task<IReadOnlyList<Department>> GetActiveAsync(CancellationToken ct = default) =>
        await context.Departments.AsNoTracking().Where(x => x.IsActive).OrderBy(x => x.DepartmentName).ToListAsync(ct);

    public async Task<IReadOnlyList<Department>> SearchAsync(string name, CancellationToken ct = default) =>
        await context.Departments.AsNoTracking().Where(x => x.IsActive && EF.Functions.ILike(x.DepartmentName, $"%{name}%")).OrderBy(x => x.DepartmentName).ToListAsync(ct);

    public Task<Department?> GetByIdAsync(int id, bool includeProjects = false, CancellationToken ct = default)
    {
        IQueryable<Department> query = context.Departments;
        if (includeProjects) query = query.Include(x => x.Projects.Where(p => p.IsActive));
        return query.FirstOrDefaultAsync(x => x.DepartmentId == id && x.IsActive, ct);
    }

    public Task<bool> HasProjectsAsync(int id, CancellationToken ct = default) => context.Projects.AnyAsync(x => x.DepartmentId == id, ct);
    public Task AddAsync(Department entity, CancellationToken ct = default) => context.Departments.AddAsync(entity, ct).AsTask();
    public void Remove(Department entity) => context.Departments.Remove(entity);
    public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}

public sealed class ProjectRepository(TaskManagementDbContext context) : IProjectRepository
{
    private IQueryable<Project> ActiveWithDepartment() => context.Projects.AsNoTracking().Include(x => x.Department).Where(x => x.IsActive);

    public async Task<IReadOnlyList<Project>> GetActiveAsync(CancellationToken ct = default) =>
        await ActiveWithDepartment().OrderByDescending(x => x.CreatedDate).ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> GetByDepartmentAsync(int departmentId, CancellationToken ct = default) =>
        await ActiveWithDepartment().Where(x => x.DepartmentId == departmentId).OrderBy(x => x.ProjectName).ToListAsync(ct);

    public async Task<IReadOnlyList<Project>> SearchAsync(string? name, short? status, int? departmentId, CancellationToken ct = default)
    {
        var query = ActiveWithDepartment();
        if (!string.IsNullOrWhiteSpace(name)) query = query.Where(x => EF.Functions.ILike(x.ProjectName, $"%{name.Trim()}%"));
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (departmentId.HasValue) query = query.Where(x => x.DepartmentId == departmentId);
        return await query.OrderBy(x => x.ProjectName).ToListAsync(ct);
    }

    public Task<Project?> GetByIdAsync(int id, bool includeTasks = false, CancellationToken ct = default)
    {
        IQueryable<Project> query = context.Projects.Include(x => x.Department);
        if (includeTasks) query = query.Include(x => x.Tasks.Where(t => t.IsActive)).ThenInclude(t => t.Tags);
        return query.FirstOrDefaultAsync(x => x.ProjectId == id && x.IsActive, ct);
    }

    public Task<bool> HasTasksAsync(int id, CancellationToken ct = default) => context.Tasks.AnyAsync(x => x.ProjectId == id, ct);
    public Task AddAsync(Project entity, CancellationToken ct = default) => context.Projects.AddAsync(entity, ct).AsTask();
    public void Remove(Project entity) => context.Projects.Remove(entity);
    public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}

public sealed class TaskRepository(TaskManagementDbContext context) : ITaskRepository
{
    private IQueryable<TaskItem> ActiveQuery() => context.Tasks.AsNoTracking().Include(x => x.Project).Include(x => x.Tags).Where(x => x.IsActive);

    public async Task<IReadOnlyList<TaskItem>> GetActiveAsync(CancellationToken ct = default) =>
        await ActiveQuery().OrderBy(x => x.DueDate).ThenBy(x => x.Title).ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> GetByProjectAsync(int projectId, CancellationToken ct = default) =>
        await ActiveQuery().Where(x => x.ProjectId == projectId).OrderBy(x => x.DueDate).ToListAsync(ct);

    public async Task<IReadOnlyList<TaskItem>> SearchAsync(string? title, short? status, short? priority, int? projectId, int? tagId, CancellationToken ct = default)
    {
        var query = ActiveQuery();
        if (!string.IsNullOrWhiteSpace(title)) query = query.Where(x => EF.Functions.ILike(x.Title, $"%{title.Trim()}%"));
        if (status.HasValue) query = query.Where(x => x.Status == status);
        if (priority.HasValue) query = query.Where(x => x.Priority == priority);
        if (projectId.HasValue) query = query.Where(x => x.ProjectId == projectId);
        if (tagId.HasValue) query = query.Where(x => x.Tags.Any(t => t.TagId == tagId));
        return await query.OrderBy(x => x.DueDate).ThenBy(x => x.Title).ToListAsync(ct);
    }

    public Task<TaskItem?> GetByIdAsync(int id, CancellationToken ct = default) =>
        context.Tasks.Include(x => x.Project).Include(x => x.Tags).FirstOrDefaultAsync(x => x.TaskId == id && x.IsActive, ct);

    public Task AddAsync(TaskItem entity, CancellationToken ct = default) => context.Tasks.AddAsync(entity, ct).AsTask();
    public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}

public sealed class TagRepository(TaskManagementDbContext context) : ITagRepository
{
    public async Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken ct = default) =>
        await context.Tags.AsNoTracking().OrderBy(x => x.TagName).ToListAsync(ct);

    public async Task<IReadOnlyList<Tag>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken ct = default)
    {
        var values = ids.Distinct().ToArray();
        return await context.Tags.Where(x => values.Contains(x.TagId)).ToListAsync(ct);
    }

    public Task<Tag?> GetByIdAsync(int id, CancellationToken ct = default) => context.Tags.FirstOrDefaultAsync(x => x.TagId == id, ct);

    public Task<bool> NameExistsAsync(string name, int? excludingId = null, CancellationToken ct = default) =>
        context.Tags.AnyAsync(x => x.TagName.ToLower() == name.ToLower() && (!excludingId.HasValue || x.TagId != excludingId), ct);

    public Task<bool> IsUsedAsync(int id, CancellationToken ct = default) => context.Tags.AnyAsync(x => x.TagId == id && x.Tasks.Any(), ct);
    public Task AddAsync(Tag entity, CancellationToken ct = default) => context.Tags.AddAsync(entity, ct).AsTask();
    public void Remove(Tag entity) => context.Tags.Remove(entity);
    public Task SaveChangesAsync(CancellationToken ct = default) => context.SaveChangesAsync(ct);
}
