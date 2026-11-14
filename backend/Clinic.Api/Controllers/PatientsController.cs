using System.Security.Claims;
using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/patients")]
public class PatientsController : ControllerBase
{
    private readonly IPatientService _service;

    public PatientsController(IPatientService service)
    {
        _service = service;
    }

    private int UserId => int.Parse(User.FindFirstValue("sub")!);
    private string RoleName => User.FindFirstValue("role")!;

    [Authorize(Roles = RoleNames.Receptionist)]
    [HttpPost]
    public async Task<ActionResult<PatientDto>> CreateWalkIn(CreatePatientRequest request)
        => StatusCode(StatusCodes.Status201Created, await _service.CreateWalkInAsync(request));

    [Authorize(Roles = $"{RoleNames.Receptionist},{RoleNames.Doctor}")]
    [HttpGet]
    public async Task<ActionResult<List<PatientDto>>> GetList([FromQuery] string? keyword)
        => Ok(await _service.GetListAsync(UserId, RoleName, keyword));

    [Authorize(Roles = $"{RoleNames.Receptionist},{RoleNames.Doctor}")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<PatientDto>> GetById(int id)
        => Ok(await _service.GetByIdAsync(id, UserId, RoleName));
}