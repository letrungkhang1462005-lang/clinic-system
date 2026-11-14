using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/dashboard")]
[Authorize(Roles = RoleNames.Admin)]
public class DashboardController : ControllerBase
{
    private readonly IDashboardService _service;

    public DashboardController(IDashboardService service)
    {
        _service = service;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<DashboardSummaryDto>> Summary([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        => Ok(await _service.GetSummaryAsync(from, to));

    [HttpGet("revenue-by-day")]
    public async Task<ActionResult<List<RevenuePointDto>>> RevenueByDay([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        => Ok(await _service.GetRevenueByDayAsync(from, to));

    [HttpGet("top-doctors")]
    public async Task<ActionResult<List<TopDoctorDto>>> TopDoctors(
        [FromQuery] DateOnly? from, [FromQuery] DateOnly? to, [FromQuery] int limit = 5)
        => Ok(await _service.GetTopDoctorsAsync(from, to, limit));

    [HttpGet("peak-hours")]
    public async Task<ActionResult<List<PeakHourDto>>> PeakHours([FromQuery] DateOnly? from, [FromQuery] DateOnly? to)
        => Ok(await _service.GetPeakHoursAsync(from, to));
}