using System.Security.Claims;
using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/services")]
public class ServicesController : ControllerBase
{
    private readonly IServiceItemService _service;

    public ServicesController(IServiceItemService service)
    {
        _service = service;
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    [HttpGet]
    public async Task<ActionResult<List<ServiceDto>>> GetList([FromQuery] bool includeInactive = false)
    {
        var all = includeInactive && User.FindFirstValue("role") == RoleNames.Admin;
        return Ok(await _service.GetListAsync(all));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    public async Task<ActionResult<ServiceDto>> Create(ServiceRequest request)
        => StatusCode(StatusCodes.Status201Created, await _service.CreateAsync(request));

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, ServiceRequest request)
    {
        await _service.UpdateAsync(id, request);
        return NoContent();
    }
}