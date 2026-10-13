using TaskTrack.Repo.Interfaces;
using TaskTrack.Repo.Models;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Exceptions;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.Service.Services;

internal static class DtoMapper
{
    public static TagDto ToDto(this Tag x) => new(x.TagId, x.TagName, x.Color);
    public static DepartmentListDto ToListDto(this Department x) => new(x.DepartmentId, x.DepartmentName, x.DepartmentDescription);
    public static ProjectListDto ToListDto(this Project x) => new(x.ProjectId, x.ProjectName, x.Description, x.StartDate, x.EndDate, x.Status, x.DepartmentId, x.Department.DepartmentName, x.CreatedDate) { CreatedById = x.CreatedById, UpdatedById = x.UpdatedById };
    public static TaskListDto ToListDto(this TaskItem x) => new(x.TaskId, x.Title, x.Description, x.Status, x.Priority, x.DueDate, x.ProjectId, x.Project.ProjectName, x.CreatedDate, x.ModifiedDate, x.Tags.OrderBy(t => t.TagName).Select(t => t.ToDto()).ToList()) { CreatedById = x.CreatedById, UpdatedById = x.UpdatedById };
    public static TaskDetailDto ToDetailDto(this TaskItem x) => new(x.TaskId, x.Title, x.Description, x.Status, x.Priority, x.DueDate, x.ProjectId, x.Project.ProjectName, x.CreatedDate, x.ModifiedDate, x.Tags.OrderBy(t => t.TagName).Select(t => t.ToDto()).ToList()) { CreatedById = x.CreatedById, UpdatedById = x.UpdatedById };
}

public sealed class DepartmentService(IDepartmentRepository repository) : IDepartmentService
{
    public async Task<IReadOnlyList<DepartmentListDto>> GetAllAsync(CancellationToken ct = default) => (await repository.GetActiveAsync(ct)).Select(x => x.ToListDto()).ToList();
    public async Task<IReadOnlyList<DepartmentListDto>> SearchAsync(string name, CancellationToken ct = default) => string.IsNullOrWhiteSpace(name) ? await GetAllAsync(ct) : (await repository.SearchAsync(name.Trim(), ct)).Select(x => x.ToListDto()).ToList();

    public async Task<DepartmentDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, true, ct) ?? throw new ResourceNotFoundException("Department was not found.");
        var projects = entity.Projects.OrderBy(x => x.ProjectName).Select(x => new ProjectListDto(x.ProjectId, x.ProjectName, x.Description, x.StartDate, x.EndDate, x.Status, x.DepartmentId, entity.DepartmentName, x.CreatedDate) { CreatedById = x.CreatedById, UpdatedById = x.UpdatedById }).ToList();
        return new(entity.DepartmentId, entity.DepartmentName, entity.DepartmentDescription, projects);
    }

    public async Task<DepartmentListDto> CreateAsync(DepartmentRequest request, CancellationToken ct = default)
    {
        var entity = new Department { DepartmentName = request.DepartmentName.Trim(), DepartmentDescription = request.DepartmentDescription.Trim(), IsActive = true };
        await repository.AddAsync(entity, ct);
        await repository.SaveChangesAsync(ct);
        return entity.ToListDto();
    }

    public async Task UpdateAsync(int id, DepartmentRequest request, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, false, ct) ?? throw new ResourceNotFoundException("Department was not found.");
        entity.DepartmentName = request.DepartmentName.Trim();
        entity.DepartmentDescription = request.DepartmentDescription.Trim();
        await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, false, ct) ?? throw new ResourceNotFoundException("Department was not found.");
        if (await repository.HasProjectsAsync(id, ct)) throw new ServiceValidationException("departmentId", "Cannot delete a department that has linked projects.");
        repository.Remove(entity);
        await repository.SaveChangesAsync(ct);
    }
}

public sealed class ProjectService(IProjectRepository repository, IDepartmentRepository departments, ICurrentAccount current) : IProjectService
{
    public async Task<IReadOnlyList<ProjectListDto>> GetAllAsync(CancellationToken ct = default) => (await repository.GetActiveAsync(ct)).Select(x => x.ToListDto()).ToList();
    public async Task<IReadOnlyList<ProjectListDto>> GetByDepartmentAsync(int departmentId, CancellationToken ct = default) => (await repository.GetByDepartmentAsync(departmentId, ct)).Select(x => x.ToListDto()).ToList();

    public async Task<IReadOnlyList<ProjectListDto>> SearchAsync(string? name, short? status, int? departmentId, CancellationToken ct = default)
    {
        if (status is < 0 or > 3) throw new ServiceValidationException("status", "Status must be between 0 and 3.");
        return (await repository.SearchAsync(name, status, departmentId, ct)).Select(x => x.ToListDto()).ToList();
    }

    public async Task<ProjectDetailDto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        var x = await repository.GetByIdAsync(id, true, ct) ?? throw new ResourceNotFoundException("Project was not found.");
        return new(x.ProjectId, x.ProjectName, x.Description, x.StartDate, x.EndDate, x.Status, x.DepartmentId, x.Department.DepartmentName, x.CreatedDate, x.Tasks.OrderBy(t => t.DueDate).Select(t => t.ToListDto()).ToList()) { CreatedById = x.CreatedById, UpdatedById = x.UpdatedById };
    }

    public async Task<ProjectListDto> CreateAsync(ProjectRequest request, CancellationToken ct = default)
    {
        var department = await departments.GetByIdAsync(request.DepartmentId, false, ct) ?? throw new ServiceValidationException("departmentId", "An active department with this ID does not exist.");
        var entity = new Project { CreatedById = current.AccountId, ProjectName = request.ProjectName.Trim(), Description = request.Description?.Trim(), StartDate = request.StartDate, EndDate = request.EndDate, Status = request.Status, DepartmentId = request.DepartmentId, Department = department, IsActive = true };
        await repository.AddAsync(entity, ct);
        await repository.SaveChangesAsync(ct);
        return entity.ToListDto();
    }

    public async Task UpdateAsync(int id, ProjectRequest request, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, false, ct) ?? throw new ResourceNotFoundException("Project was not found.");
        var department = await departments.GetByIdAsync(request.DepartmentId, false, ct) ?? throw new ServiceValidationException("departmentId", "An active department with this ID does not exist.");
        entity.UpdatedById = current.AccountId; entity.ProjectName = request.ProjectName.Trim(); entity.Description = request.Description?.Trim(); entity.StartDate = request.StartDate; entity.EndDate = request.EndDate; entity.Status = request.Status; entity.DepartmentId = request.DepartmentId; entity.Department = department;
        await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, false, ct) ?? throw new ResourceNotFoundException("Project was not found.");
        if (await repository.HasTasksAsync(id, ct)) throw new ServiceValidationException("projectId", "Cannot delete a project that has linked tasks.");
        repository.Remove(entity);
        await repository.SaveChangesAsync(ct);
    }
}

public sealed class TaskService(ITaskRepository repository, IProjectRepository projects, ITagRepository tags, ICurrentAccount current) : ITaskService
{
    public async Task<IReadOnlyList<TaskListDto>> GetAllAsync(CancellationToken ct = default) => (await repository.GetActiveAsync(ct)).Select(x => x.ToListDto()).ToList();
    public async Task<IReadOnlyList<TaskListDto>> GetByProjectAsync(int projectId, CancellationToken ct = default) => (await repository.GetByProjectAsync(projectId, ct)).Select(x => x.ToListDto()).ToList();

    public async Task<IReadOnlyList<TaskListDto>> SearchAsync(string? title, short? status, short? priority, int? projectId, int? tagId, CancellationToken ct = default)
    {
        if (status is < 0 or > 3) throw new ServiceValidationException("status", "Status must be between 0 and 3.");
        if (priority is < 0 or > 3) throw new ServiceValidationException("priority", "Priority must be between 0 and 3.");
        return (await repository.SearchAsync(title, status, priority, projectId, tagId, ct)).Select(x => x.ToListDto()).ToList();
    }

    public async Task<TaskDetailDto> GetByIdAsync(int id, CancellationToken ct = default) => (await repository.GetByIdAsync(id, ct) ?? throw new ResourceNotFoundException("Task was not found.")).ToDetailDto();

    public async Task<TaskDetailDto> CreateAsync(TaskRequest request, CancellationToken ct = default)
    {
        var project = await projects.GetByIdAsync(request.ProjectId, false, ct) ?? throw new ServiceValidationException("projectId", "An active project with this ID does not exist.");
        var selectedTags = await ValidateTagsAsync(request.TagIds, ct);
        var entity = new TaskItem { CreatedById = current.AccountId, Title = request.Title.Trim(), Description = request.Description?.Trim(), Status = request.Status, Priority = request.Priority, DueDate = request.DueDate, ProjectId = request.ProjectId, Project = project, IsActive = true };
        foreach (var tag in selectedTags) entity.Tags.Add(tag);
        await repository.AddAsync(entity, ct);
        await repository.SaveChangesAsync(ct);
        return entity.ToDetailDto();
    }

    public async Task UpdateAsync(int id, TaskRequest request, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct) ?? throw new ResourceNotFoundException("Task was not found.");
        var project = await projects.GetByIdAsync(request.ProjectId, false, ct) ?? throw new ServiceValidationException("projectId", "An active project with this ID does not exist.");
        var selectedTags = await ValidateTagsAsync(request.TagIds, ct);
        entity.UpdatedById = current.AccountId; entity.Title = request.Title.Trim(); entity.Description = request.Description?.Trim(); entity.Status = request.Status; entity.Priority = request.Priority; entity.DueDate = request.DueDate; entity.ProjectId = request.ProjectId; entity.Project = project; entity.ModifiedDate = DateTime.Now;
        entity.Tags.Clear(); foreach (var tag in selectedTags) entity.Tags.Add(tag);
        await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct) ?? throw new ResourceNotFoundException("Task was not found.");
        entity.UpdatedById = current.AccountId; entity.IsActive = false; entity.ModifiedDate = DateTime.Now;
        await repository.SaveChangesAsync(ct);
    }

    private async Task<IReadOnlyList<Tag>> ValidateTagsAsync(IEnumerable<int> ids, CancellationToken ct)
    {
        var requested = ids.Distinct().ToArray();
        if (requested.Any(x => x <= 0)) throw new ServiceValidationException("tagIds", "Tag IDs must be positive integers.");
        var selected = await tags.GetByIdsAsync(requested, ct);
        if (selected.Count != requested.Length) throw new ServiceValidationException("tagIds", "One or more tag IDs do not exist.");
        return selected;
    }
}

public sealed class TagService(ITagRepository repository) : ITagService
{
    public async Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken ct = default) => (await repository.GetAllAsync(ct)).Select(x => x.ToDto()).ToList();

    public async Task<TagDto> CreateAsync(TagRequest request, CancellationToken ct = default)
    {
        var name = request.TagName.Trim();
        if (await repository.NameExistsAsync(name, null, ct)) throw new ServiceValidationException("tagName", "Tag name already exists.");
        var entity = new Tag { TagName = name, Color = request.Color?.ToUpperInvariant() };
        await repository.AddAsync(entity, ct); await repository.SaveChangesAsync(ct); return entity.ToDto();
    }

    public async Task UpdateAsync(int id, TagRequest request, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct) ?? throw new ResourceNotFoundException("Tag was not found.");
        var name = request.TagName.Trim();
        if (await repository.NameExistsAsync(name, id, ct)) throw new ServiceValidationException("tagName", "Tag name already exists.");
        entity.TagName = name; entity.Color = request.Color?.ToUpperInvariant(); await repository.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        var entity = await repository.GetByIdAsync(id, ct) ?? throw new ResourceNotFoundException("Tag was not found.");
        if (await repository.IsUsedAsync(id, ct)) throw new ServiceValidationException("tagId", "Cannot delete a tag that is used by a task.");
        repository.Remove(entity); await repository.SaveChangesAsync(ct);
    }
}
