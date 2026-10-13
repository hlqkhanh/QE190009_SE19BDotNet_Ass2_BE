using System.ComponentModel.DataAnnotations;

namespace TaskTrack.Service.DTOs;

[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field | AttributeTargets.Parameter)]
public sealed class NotWhiteSpaceAttribute : ValidationAttribute
{
    public override bool IsValid(object? value) => value is not string text || !string.IsNullOrWhiteSpace(text);
}

public sealed record TagDto(int TagId, string TagName, string? Color);
public sealed record DepartmentListDto(int DepartmentId, string DepartmentName, string DepartmentDescription);
public sealed record ProjectListDto(int ProjectId, string ProjectName, string? Description, DateOnly StartDate, DateOnly? EndDate, short Status, int DepartmentId, string DepartmentName, DateTime CreatedDate) { public int? CreatedById { get; init; } public int? UpdatedById { get; init; } }
public sealed record TaskListDto(int TaskId, string Title, string? Description, short Status, short Priority, DateOnly? DueDate, int ProjectId, string ProjectName, DateTime CreatedDate, DateTime? ModifiedDate, IReadOnlyList<TagDto> Tags) { public int? CreatedById { get; init; } public int? UpdatedById { get; init; } }
public sealed record DepartmentDetailDto(int DepartmentId, string DepartmentName, string DepartmentDescription, IReadOnlyList<ProjectListDto> Projects);
public sealed record ProjectDetailDto(int ProjectId, string ProjectName, string? Description, DateOnly StartDate, DateOnly? EndDate, short Status, int DepartmentId, string DepartmentName, DateTime CreatedDate, IReadOnlyList<TaskListDto> Tasks) { public int? CreatedById { get; init; } public int? UpdatedById { get; init; } }
public sealed record TaskDetailDto(int TaskId, string Title, string? Description, short Status, short Priority, DateOnly? DueDate, int ProjectId, string ProjectName, DateTime CreatedDate, DateTime? ModifiedDate, IReadOnlyList<TagDto> Tags) { public int? CreatedById { get; init; } public int? UpdatedById { get; init; } }

public sealed class DepartmentRequest
{
    [Required, NotWhiteSpace(ErrorMessage = "Department name cannot be empty or whitespace."), StringLength(100)] public string DepartmentName { get; set; } = string.Empty;
    [Required, NotWhiteSpace(ErrorMessage = "Department description cannot be empty or whitespace."), StringLength(300)] public string DepartmentDescription { get; set; } = string.Empty;
}

public sealed class ProjectRequest : IValidatableObject
{
    [Required, NotWhiteSpace(ErrorMessage = "Project name cannot be empty or whitespace."), StringLength(200)] public string ProjectName { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly? EndDate { get; set; }
    [Range(0, 3)] public short Status { get; set; }
    [Range(1, int.MaxValue)] public int DepartmentId { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (StartDate == default)
            yield return new ValidationResult("Start date is required.", [nameof(StartDate)]);
        else if (EndDate.HasValue && EndDate.Value < StartDate)
            yield return new ValidationResult("End date cannot be earlier than start date.", [nameof(EndDate)]);
    }
}

public sealed class TaskRequest
{
    [Required, NotWhiteSpace(ErrorMessage = "Task title cannot be empty or whitespace."), StringLength(300)] public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    [Range(0, 3)] public short Status { get; set; }
    [Range(0, 3)] public short Priority { get; set; } = 1;
    public DateOnly? DueDate { get; set; }
    [Range(1, int.MaxValue)] public int ProjectId { get; set; }
    public IReadOnlyList<int> TagIds { get; set; } = [];
}

public sealed class TagRequest
{
    [Required, NotWhiteSpace(ErrorMessage = "Tag name cannot be empty or whitespace."), StringLength(50)] public string TagName { get; set; } = string.Empty;
    [RegularExpression("^#[0-9A-Fa-f]{6}$", ErrorMessage = "Color must be a hex value such as #3B82F6.")]
    public string? Color { get; set; }
}
