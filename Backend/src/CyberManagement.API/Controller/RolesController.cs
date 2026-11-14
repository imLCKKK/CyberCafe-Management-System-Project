using CyberManagement.API.Security;
using CyberManagement.Application.Interfaces.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CyberManagement.API.Controller;

[ApiController]
[Route("api/admin/roles")]
[Authorize(Policy = ApiPolicies.RoleManage)]
public sealed class RolesController(IIdentityService identityService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRoles(CancellationToken cancellationToken) =>
        Ok(await identityService.GetRolesAsync(cancellationToken));
}
