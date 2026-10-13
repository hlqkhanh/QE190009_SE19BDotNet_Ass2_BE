using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.API.Controllers;

[ApiController, Route("api/projects")]
public sealed class ProjectsController(IProjectService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<ProjectListDto>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));
    [HttpGet("{id:int}")] public async Task<ActionResult<ProjectDetailDto>> GetById(int id, CancellationToken ct) => Ok(await service.GetByIdAsync(id, ct));
    [HttpGet("department/{departmentId:int}")] public async Task<ActionResult<IReadOnlyList<ProjectListDto>>> GetByDepartment(int departmentId, CancellationToken ct) => Ok(await service.GetByDepartmentAsync(departmentId, ct));
    [HttpGet("search")] public async Task<ActionResult<IReadOnlyList<ProjectListDto>>> Search([FromQuery] string? name, [FromQuery] short? status, [FromQuery] int? departmentId, CancellationToken ct) => Ok(await service.SearchAsync(name, status, departmentId, ct));

    [Authorize, HttpPost]
    public async Task<ActionResult<ProjectListDto>> Create(ProjectRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.ProjectId }, created);
    }

    [Authorize, HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, ProjectRequest request, CancellationToken ct) { await service.UpdateAsync(id, request, ct); return NoContent(); }
    [Authorize, HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}
