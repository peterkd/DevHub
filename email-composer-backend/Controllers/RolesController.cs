using EmailComposer.Backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace EmailComposer.Backend.Controllers;

[ApiController]
[Route("api/roles")]
public sealed class RolesController : ControllerBase
{
    private readonly OrganizationRoleService _organizationRoleService;

    public RolesController(OrganizationRoleService organizationRoleService)
    {
        _organizationRoleService = organizationRoleService;
    }

    [HttpGet("organization-roles")]
    public async Task<IActionResult> GetOrganizationRoles(CancellationToken cancellationToken)
    {
        try
        {
            var roles = await _organizationRoleService.GetOrganizationRolesAsync(cancellationToken);
            return Ok(roles);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
