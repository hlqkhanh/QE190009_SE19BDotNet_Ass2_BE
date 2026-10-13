using TaskTrack.Repo.Models;

namespace TaskTrack.Repo.Interfaces;

public interface IDepartmentRepository
{
    Task<IReadOnlyList<Department>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Department>> SearchAsync(string name, CancellationToken cancellationToken = default);
    Task<Department?> GetByIdAsync(int id, bool includeProjects = false, CancellationToken cancellationToken = default);
    Task<bool> HasProjectsAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Department department, CancellationToken cancellationToken = default);
    void Remove(Department department);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface IProjectRepository
{
    Task<IReadOnlyList<Project>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Project>> SearchAsync(string? name, short? status, int? departmentId, CancellationToken cancellationToken = default);
    Task<Project?> GetByIdAsync(int id, bool includeTasks = false, CancellationToken cancellationToken = default);
    Task<bool> HasTasksAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Project project, CancellationToken cancellationToken = default);
    void Remove(Project project);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ITaskRepository
{
    Task<IReadOnlyList<TaskItem>> GetActiveAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskItem>> SearchAsync(string? title, short? status, short? priority, int? projectId, int? tagId, CancellationToken cancellationToken = default);
    Task<TaskItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(TaskItem task, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}

public interface ITagRepository
{
    Task<IReadOnlyList<Tag>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Tag>> GetByIdsAsync(IEnumerable<int> ids, CancellationToken cancellationToken = default);
    Task<Tag?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<bool> NameExistsAsync(string name, int? excludingId = null, CancellationToken cancellationToken = default);
    Task<bool> IsUsedAsync(int id, CancellationToken cancellationToken = default);
    Task AddAsync(Tag tag, CancellationToken cancellationToken = default);
    void Remove(Tag tag);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
