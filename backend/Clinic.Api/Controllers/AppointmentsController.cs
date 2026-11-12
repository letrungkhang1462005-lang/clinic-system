using System.Security.Claims;
using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Enums;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/appointments")]
public class AppointmentsController : ControllerBase
{
    private readonly IAppointmentService _service;

    public AppointmentsController(IAppointmentService service)
    {
        _service = service;
    }

    private int UserId => int.Parse(User.FindFirstValue("sub")!);
    private string RoleName => User.FindFirstValue("role")!;

    [Authorize(Roles = $"{RoleNames.Patient},{RoleNames.Receptionist}")]
    [HttpPost]
    public async Task<ActionResult<AppointmentDto>> Create(CreateAppointmentRequest request)
        => StatusCode(StatusCodes.Status201Created, await _service.CreateAsync(UserId, RoleName, request));

    [Authorize(Roles = RoleNames.Patient)]
    [HttpGet("my")]
    public async Task<ActionResult<List<AppointmentDto>>> GetMy([FromQuery] AppointmentStatus? status)
        => Ok(await _service.GetMyAsync(UserId, status));

    [Authorize(Roles = $"{RoleNames.Receptionist},{RoleNames.Doctor}")]
    [HttpGet]
    public async Task<ActionResult<List<AppointmentDto>>> GetList(
        [FromQuery] DateOnly? date, [FromQuery] int? doctorId, [FromQuery] AppointmentStatus? status)
        => Ok(await _service.GetListAsync(UserId, RoleName, date, doctorId, status));

    [Authorize]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<AppointmentDto>> GetById(int id)
        => Ok(await _service.GetByIdAsync(id, UserId, RoleName));

    [Authorize(Roles = RoleNames.Receptionist)]
    [HttpPut("{id:int}/confirm")]
    public async Task<ActionResult<AppointmentDto>> Confirm(int id)
        => Ok(await _service.ConfirmAsync(id));

    [Authorize(Roles = RoleNames.Receptionist)]
    [HttpPut("{id:int}/check-in")]
    public async Task<ActionResult<AppointmentDto>> CheckIn(int id)
        => Ok(await _service.CheckInAsync(id));

    [Authorize(Roles = $"{RoleNames.Patient},{RoleNames.Receptionist}")]
    [HttpPut("{id:int}/cancel")]
    public async Task<ActionResult<AppointmentDto>> Cancel(int id, CancelAppointmentRequest? request)
        => Ok(await _service.CancelAsync(id, UserId, RoleName, request?.Reason));
}