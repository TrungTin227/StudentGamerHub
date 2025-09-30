namespace DTOs.Roles.Requests
{
    public sealed record CreateRoleRequest(
         string?  Name,
         string? Description
    );

    public sealed record UpdateRoleRequest(
         string? Name,
         string? Description
    );
}
