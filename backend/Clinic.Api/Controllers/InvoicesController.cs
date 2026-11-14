using System.Security.Claims;
using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Enums;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/invoices")]
public class InvoicesController : ControllerBase
{
    private readonly IInvoiceService _service;

    public InvoicesController(IInvoiceService service)
    {
        _service = service;
    }

    private int UserId => int.Parse(User.FindFirstValue("sub")!);
    private string RoleName => User.FindFirstValue("role")!;

    [Authorize(Roles = RoleNames.Receptionist)]
    [HttpPost]
    public async Task<ActionResult<InvoiceDto>> Create(CreateInvoiceRequest request)
        => StatusCode(StatusCodes.Status201Created, await _service.CreateAsync(UserId, request));

    [Authorize(Roles = $"{RoleNames.Receptionist},{RoleNames.Admin}")]
    [HttpGet]
    public async Task<ActionResult<List<InvoiceDto>>> GetList([FromQuery] DateOnly? date, [FromQuery] InvoiceStatus? status)
        => Ok(await _service.GetListAsync(date, status));

    [Authorize(Roles = RoleNames.Patient)]
    [HttpGet("my")]
    public async Task<ActionResult<List<InvoiceDto>>> GetMy()
        => Ok(await _service.GetMyAsync(UserId));

    [Authorize(Roles = $"{RoleNames.Receptionist},{RoleNames.Admin},{RoleNames.Patient}")]
    [HttpGet("{id:int}")]
    public async Task<ActionResult<InvoiceDto>> GetById(int id)
        => Ok(await _service.GetByIdAsync(id, UserId, RoleName));

    [Authorize(Roles = RoleNames.Receptionist)]
    [HttpPut("{id:int}/pay")]
    public async Task<ActionResult<InvoiceDto>> Pay(int id, PayInvoiceRequest request)
        => Ok(await _service.PayAsync(id, request.PaymentMethod));
}