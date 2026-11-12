using System.Security.Claims;
using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
public class ExaminationController : ControllerBase
{
    private readonly IExaminationService _service;

    public ExaminationController(IExaminationService service)
    {
        _service = service;
    }

    private int UserId => int.Parse(User.FindFirstValue("sub")!);
    private string RoleName => User.FindFirstValue("role")!;

    // ----- Bệnh án -----
    [Authorize(Roles = RoleNames.Doctor)]
    [HttpPost("api/medical-records")]
    public async Task<ActionResult<MedicalRecordDto>> CreateRecord(CreateMedicalRecordRequest request)
        => StatusCode(StatusCodes.Status201Created, await _service.CreateRecordAsync(UserId, request));

    [Authorize(Roles = RoleNames.Doctor)]
    [HttpPut("api/medical-records/{id:int}")]
    public async Task<ActionResult<MedicalRecordDto>> UpdateRecord(int id, UpdateMedicalRecordRequest request)
        => Ok(await _service.UpdateRecordAsync(id, UserId, request));

    [Authorize(Roles = $"{RoleNames.Doctor},{RoleNames.Patient}")]
    [HttpGet("api/medical-records/{id:int}")]
    public async Task<ActionResult<MedicalRecordDto>> GetRecord(int id)
        => Ok(await _service.GetRecordAsync(id, UserId, RoleName));

    // ----- Đơn thuốc -----
    [Authorize(Roles = RoleNames.Doctor)]
    [HttpPost("api/prescriptions")]
    public async Task<ActionResult<PrescriptionDto>> CreatePrescription(CreatePrescriptionRequest request)
        => StatusCode(StatusCodes.Status201Created, await _service.CreatePrescriptionAsync(UserId, request));

    [Authorize(Roles = $"{RoleNames.Doctor},{RoleNames.Patient}")]
    [HttpGet("api/prescriptions/{id:int}")]
    public async Task<ActionResult<PrescriptionDto>> GetPrescription(int id)
        => Ok(await _service.GetPrescriptionAsync(id, UserId, RoleName));

    // ----- Lịch sử khám -----
    [Authorize(Roles = $"{RoleNames.Doctor},{RoleNames.Patient}")]
    [HttpGet("api/patients/{id:int}/history")]
    public async Task<ActionResult<List<MedicalRecordDto>>> GetHistory(int id)
        => Ok(await _service.GetHistoryAsync(id, UserId, RoleName));

    [Authorize(Roles = RoleNames.Patient)]
    [HttpGet("api/patients/me/history")]
    public async Task<ActionResult<List<MedicalRecordDto>>> GetMyHistory()
        => Ok(await _service.GetMyHistoryAsync(UserId));
}