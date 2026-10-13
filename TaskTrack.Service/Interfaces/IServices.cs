using TaskTrack.Service.DTOs;

namespace TaskTrack.Service.Interfaces;

public interface IDepartmentService
{
    Task<IReadOnlyList<DepartmentListDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DepartmentListDto>> SearchAsync(string name, CancellationToken cancellationToken = default);
    Task<DepartmentDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<DepartmentListDto> CreateAsync(DepartmentRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, DepartmentRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface IProjectService
{
    Task<IReadOnlyList<ProjectListDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectListDto>> GetByDepartmentAsync(int departmentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProjectListDto>> SearchAsync(string? name, short? status, int? departmentId, CancellationToken cancellationToken = default);
    Task<ProjectDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<ProjectListDto> CreateAsync(ProjectRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, ProjectRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface ITaskService
{
    Task<IReadOnlyList<TaskListDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskListDto>> GetByProjectAsync(int projectId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaskListDto>> SearchAsync(string? title, short? status, short? priority, int? projectId, int? tagId, CancellationToken cancellationToken = default);
    Task<TaskDetailDto> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<TaskDetailDto> CreateAsync(TaskRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, TaskRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}

public interface ITagService
{
    Task<IReadOnlyList<TagDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<TagDto> CreateAsync(TagRequest request, CancellationToken cancellationToken = default);
    Task UpdateAsync(int id, TagRequest request, CancellationToken cancellationToken = default);
    Task DeleteAsync(int id, CancellationToken cancellationToken = default);
}
