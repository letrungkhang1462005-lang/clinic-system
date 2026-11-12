using Clinic.Core.DTOs;
using Clinic.Core.Entities;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class ServiceItemService : IServiceItemService
{
    private readonly ClinicDbContext _db;

    public ServiceItemService(ClinicDbContext db)
    {
        _db = db;
    }

    public async Task<List<ServiceDto>> GetListAsync(bool includeInactive)
    {
        var query = _db.Services.AsNoTracking().AsQueryable();
        if (!includeInactive)
            query = query.Where(s => s.IsActive);

        return await query.OrderBy(s => s.Name)
            .Select(s => new ServiceDto(s.Id, s.Name, s.Price, s.Description, s.IsActive))
            .ToListAsync();
    }

    public async Task<ServiceDto> CreateAsync(ServiceRequest request)
    {
        var name = request.Name.Trim();
        if (await _db.Services.AnyAsync(s => s.Name == name))
            throw new AppException(409, "SERVICE_EXISTS", "Dịch vụ đã tồn tại.");

        var entity = new Service
        {
            Name = name,
            Price = request.Price,
            Description = request.Description,
            IsActive = request.IsActive
        };
        _db.Services.Add(entity);
        await _db.SaveChangesAsync();

        return new ServiceDto(entity.Id, entity.Name, entity.Price, entity.Description, entity.IsActive);
    }

    public async Task UpdateAsync(int id, ServiceRequest request)
    {
        var entity = await _db.Services.FindAsync(id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy dịch vụ.");

        var name = request.Name.Trim();
        if (await _db.Services.AnyAsync(s => s.Name == name && s.Id != id))
            throw new AppException(409, "SERVICE_EXISTS", "Dịch vụ đã tồn tại.");

        entity.Name = name;
        entity.Price = request.Price;
        entity.Description = request.Description;
        entity.IsActive = request.IsActive;
        await _db.SaveChangesAsync();
    }
}