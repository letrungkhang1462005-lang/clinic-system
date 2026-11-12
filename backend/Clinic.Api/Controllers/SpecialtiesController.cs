using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Clinic.Api.Controllers;

[ApiController]
[Route("api/specialties")]
public class SpecialtiesController : ControllerBase
{
    private readonly ISpecialtyService _service;

    public SpecialtiesController(ISpecialtyService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<SpecialtyDto>>> GetAll()
        => Ok(await _service.GetAllAsync());

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPost]
    public async Task<ActionResult<SpecialtyDto>> Create(SpecialtyRequest request)
        => StatusCode(StatusCodes.Status201Created, await _service.CreateAsync(request));

    [Authorize(Roles = RoleNames.Admin)]
    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, SpecialtyRequest request)
    {
        await _service.UpdateAsync(id, request);
        return NoContent();
    }

    [Authorize(Roles = RoleNames.Admin)]
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        await _service.DeleteAsync(id);
        return NoContent();
    }
}