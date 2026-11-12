using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
public class DoctorsController : ControllerBase
{
    private readonly IDoctorService _doctors;
    private readonly IScheduleService _schedules;

    public DoctorsController(IDoctorService doctors, IScheduleService schedules)
    {
        _doctors = doctors;
        _schedules = schedules;
    }

    // ----- Bác sĩ -----
    [HttpGet("api/doctors")]
    public async Task<ActionResult<List<DoctorDto>>> GetList([FromQuery] int? specialtyId, [FromQuery] string? keyword)
        => Ok(await _doctors.GetListAsync(specialtyId, keyword));

    [HttpGet("api/doctors/{id:int}")]
    public async Task<ActionResult<DoctorDto>> GetById(int id)
        => Ok(await _doctors.GetByIdAsync(id));

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("api/doctors")]
    public async Task<ActionResult<DoctorDto>> Create(CreateDoctorRequest request)
        => StatusCode(StatusCodes.Status201Created, await _doctors.CreateAsync(request));

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("api/doctors/{id:int}")]
    public async Task<IActionResult> Update(int id, UpdateDoctorRequest request)
    {
        await _doctors.UpdateAsync(id, request);
        return NoContent();
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("api/doctors/{id:int}")]
    public async Task<IActionResult> Deactivate(int id)
    {
        await _doctors.DeactivateAsync(id);
        return NoContent();
    }

    // ----- Khung giờ trống -----
    [Authorize(Roles = $"{RoleNames.Patient},{RoleNames.Receptionist}")]
    [HttpGet("api/doctors/{id:int}/slots")]
    public async Task<ActionResult<List<SlotDto>>> GetSlots(int id, [FromQuery] DateOnly date)
    {
        if (date == default)
            throw new AppException(400, "INVALID_DATE", "Thiếu hoặc sai định dạng ngày (yyyy-MM-dd).");

        return Ok(await _schedules.GetSlotsAsync(id, date));
    }

    // ----- Lịch làm việc -----
    [Authorize(Roles = $"{RoleNames.Admin},{RoleNames.Doctor}")]
    [HttpGet("api/doctors/{id:int}/schedules")]
    public async Task<ActionResult<List<ScheduleDto>>> GetSchedules(int id)
        => Ok(await _schedules.GetByDoctorAsync(id));

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost("api/doctors/{id:int}/schedules")]
    public async Task<ActionResult<ScheduleDto>> AddSchedule(int id, ScheduleRequest request)
        => StatusCode(StatusCodes.Status201Created, await _schedules.AddAsync(id, request));

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("api/schedules/{scheduleId:int}")]
    public async Task<IActionResult> UpdateSchedule(int scheduleId, ScheduleRequest request)
    {
        await _schedules.UpdateAsync(scheduleId, request);
        return NoContent();
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("api/schedules/{scheduleId:int}")]
    public async Task<IActionResult> DeleteSchedule(int scheduleId)
    {
        await _schedules.DeleteAsync(scheduleId);
        return NoContent();
    }
}