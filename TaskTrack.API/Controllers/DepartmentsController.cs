using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TaskTrack.Service.DTOs;
using TaskTrack.Service.Interfaces;

namespace TaskTrack.API.Controllers;

[ApiController, Route("api/departments")]
public sealed class DepartmentsController(IDepartmentService service) : ControllerBase
{
    [HttpGet] public async Task<ActionResult<IReadOnlyList<DepartmentListDto>>> GetAll(CancellationToken ct) => Ok(await service.GetAllAsync(ct));
    [HttpGet("{id:int}")] public async Task<ActionResult<DepartmentDetailDto>> GetById(int id, CancellationToken ct) => Ok(await service.GetByIdAsync(id, ct));
    [HttpGet("search")] public async Task<ActionResult<IReadOnlyList<DepartmentListDto>>> Search([FromQuery] string name = "", CancellationToken ct = default) => Ok(await service.SearchAsync(name, ct));

    [Authorize, HttpPost]
    public async Task<ActionResult<DepartmentListDto>> Create(DepartmentRequest request, CancellationToken ct)
    {
        var created = await service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = created.DepartmentId }, created);
    }

    [Authorize, HttpPut("{id:int}")] public async Task<IActionResult> Update(int id, DepartmentRequest request, CancellationToken ct) { await service.UpdateAsync(id, request, ct); return NoContent(); }
    [Authorize, HttpDelete("{id:int}")] public async Task<IActionResult> Delete(int id, CancellationToken ct) { await service.DeleteAsync(id, ct); return NoContent(); }
}
