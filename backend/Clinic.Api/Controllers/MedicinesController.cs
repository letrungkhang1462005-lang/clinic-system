using System.Security.Claims;
using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/medicines")]
public class MedicinesController : ControllerBase
{
    private readonly IMedicineService _service;

    public MedicinesController(IMedicineService service)
    {
        _service = service;
    }

    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor},{RoleNames.Receptionist}")]
    [HttpGet]
    public async Task<ActionResult<List<MedicineDto>>> GetList(
        [FromQuery] string? keyword, [FromQuery] bool includeInactive = false)
    {
        // Chỉ Admin mới được xem cả thuốc đã ngừng sử dụng
        var all = includeInactive && User.FindFirstValue("role") == RoleNames.Admin;
        return Ok(await _service.GetListAsync(keyword, all));
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    public async Task<ActionResult<MedicineDto>> Create(MedicineRequest request)
        => StatusCode(StatusCodes.Status201Created, await _service.CreateAsync(request));

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, MedicineRequest request)
    {
        await _service.UpdateAsync(id, request);
        return NoContent();
    }
}