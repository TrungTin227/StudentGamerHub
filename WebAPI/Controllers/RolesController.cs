using Microsoft.AspNetCore.Mvc;

namespace WebApi.Controllers;

[ApiController]
[Route("api/[controller]")]
// [Authorize(Roles = "Admin")]
public sealed class RolesController : ControllerBase
{
    private readonly IRoleService _service;

    public RolesController(IRoleService service) => _service = service;

    // ====================== READ ======================

    /// <summary>Danh sách phân trang có filter (Keyword/Deleted/Created range).</summary>
    [HttpGet]
    [ProducesResponseType(typeof(PagedResult<RoleDto>), StatusCodes.Status200OK)]
    public async Task<ActionResult> GetPaged([FromQuery] PageRequest paging, [FromQuery] RoleFilter filter, CancellationToken ct)
    {
        // Service overload: ListAsync(PageRequest, RoleFilter?)
        var r = await _service.ListAsync(paging, filter, ct);
        return this.ToActionResult(r);
    }

    /// <summary>Lấy chi tiết role theo Id.</summary>
    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> GetById([FromRoute] Guid id, CancellationToken ct)
    {
        var r = await _service.GetByIdAsync(id, ct);
        return this.ToActionResult(r);
    }

    /// <summary>Kiểm tra trùng tên role.</summary>
    [HttpGet("exists")]
    [ProducesResponseType(typeof(bool), StatusCodes.Status200OK)]
    public async Task<ActionResult> Exists([FromQuery] string name, [FromQuery] Guid? excludeId, CancellationToken ct)
    {
        var r = await _service.NameExistsAsync(name, excludeId, ct);
        return this.ToActionResult(r);
    }

    // ====================== SINGLE MUTATIONS ======================

    /// <summary>Tạo role mới.</summary>
    [HttpPost]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Create([FromBody] CreateRoleRequest req, CancellationToken ct)
    {
        var r = await _service.CreateAsync(req, ct);

        return this.ToCreatedAtAction(
            r,
            actionName: nameof(GetById),
            routeValues: new { id = (r.IsSuccess ? r.Value!.Id : Guid.Empty) }
        );
    }

    /// <summary>Cập nhật role.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType(typeof(RoleDto), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> Update([FromRoute] Guid id, [FromBody] UpdateRoleRequest req, CancellationToken ct)
    {
        var r = await _service.UpdateAsync(id, req, ct);
        return this.ToActionResult(r);
    }

    /// <summary>Xoá cứng role.</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Delete([FromRoute] Guid id, CancellationToken ct)
    {
        var r = await _service.DeleteAsync(id, ct);
        return this.ToActionResult(r);
    }

    /// <summary>Soft delete role.</summary>
    [HttpPost("{id:guid}/soft-delete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> SoftDelete([FromRoute] Guid id, CancellationToken ct)
    {
        var r = await _service.SoftDeleteAsync(id, ct);
        return this.ToActionResult(r);
    }

    /// <summary>Khôi phục role đã soft-delete.</summary>
    [HttpPost("{id:guid}/restore")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult> Restore([FromRoute] Guid id, CancellationToken ct)
    {
        var r = await _service.RestoreAsync(id, ct);
        return this.ToActionResult(r);
    }

    // ====================== BATCH ======================

    /// <summary>Tạo nhiều role.</summary>
    [HttpPost("batch-create")]
    [ProducesResponseType(typeof(BatchResult<Guid, RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> BatchCreate([FromBody] IEnumerable<CreateRoleRequest> reqs, CancellationToken ct)
    {
        var r = await _service.CreateManyAsync(reqs, transactional: true, ct);
        return this.ToActionResult(r);
    }

    /// <summary>Cập nhật nhiều role.</summary>
    [HttpPut("batch-update")]
    [ProducesResponseType(typeof(BatchResult<Guid, RoleDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult> BatchUpdate([FromBody] IEnumerable<UpdateItem<Guid, UpdateRoleRequest>> items, CancellationToken ct)
    {
        var r = await _service.UpdateManyAsync(items, transactional: true, ct);
        return this.ToActionResult(r);
    }

    /// <summary>Xoá cứng nhiều role.</summary>
    [HttpDelete("batch-delete")]
    [ProducesResponseType(typeof(BatchOutcome<Guid>), StatusCodes.Status200OK)]
    public async Task<ActionResult> BatchDelete([FromBody] IEnumerable<Guid> ids, CancellationToken ct)
    {
        var r = await _service.DeleteManyAsync(ids, transactional: true, ct);
        return this.ToActionResult(r);
    }

    /// <summary>Soft delete nhiều role.</summary>
    [HttpPost("batch-soft-delete")]
    [ProducesResponseType(typeof(BatchOutcome<Guid>), StatusCodes.Status200OK)]
    public async Task<ActionResult> BatchSoftDelete([FromBody] IEnumerable<Guid> ids, CancellationToken ct)
    {
        var r = await _service.SoftDeleteManyAsync(ids, transactional: true, ct);
        return this.ToActionResult(r);
    }

    /// <summary>Khôi phục nhiều role đã soft-delete.</summary>
    [HttpPost("batch-restore")]
    [ProducesResponseType(typeof(BatchOutcome<Guid>), StatusCodes.Status200OK)]
    public async Task<ActionResult> BatchRestore([FromBody] IEnumerable<Guid> ids, CancellationToken ct)
    {
        var r = await _service.RestoreManyAsync(ids, transactional: true, ct);
        return this.ToActionResult(r);
    }
}
