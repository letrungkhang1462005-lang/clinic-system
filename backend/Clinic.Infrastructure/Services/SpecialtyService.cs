using Clinic.Core.DTOs;
using Clinic.Core.Entities;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class SpecialtyService : ISpecialtyService
{
    private readonly ClinicDbContext _db;

    public SpecialtyService(ClinicDbContext db)
    {
        _db = db;
    }

    public async Task<List<SpecialtyDto>> GetAllAsync() =>
        await _db.Specialties.AsNoTracking()
            .OrderBy(s => s.Name)
            .Select(s => new SpecialtyDto(s.Id, s.Name, s.Description))
            .ToListAsync();

    public async Task<SpecialtyDto> CreateAsync(SpecialtyRequest request)
    {
        var name = request.Name.Trim();
        if (await _db.Specialties.AnyAsync(s => s.Name == name))
            throw new AppException(409, "SPECIALTY_EXISTS", "Chuyên khoa đã tồn tại.");

        var entity = new Specialty { Name = name, Description = request.Description };
        _db.Specialties.Add(entity);
        await _db.SaveChangesAsync();
        return new SpecialtyDto(entity.Id, entity.Name, entity.Description);
    }

    public async Task UpdateAsync(int id, SpecialtyRequest request)
    {
        var entity = await _db.Specialties.FindAsync(id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy chuyên khoa.");

        var name = request.Name.Trim();
        if (await _db.Specialties.AnyAsync(s => s.Name == name && s.Id != id))
            throw new AppException(409, "SPECIALTY_EXISTS", "Chuyên khoa đã tồn tại.");

        entity.Name = name;
        entity.Description = request.Description;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var entity = await _db.Specialties.FindAsync(id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy chuyên khoa.");

        if (await _db.Doctors.AnyAsync(d => d.SpecialtyId == id))
            throw new AppException(409, "SPECIALTY_IN_USE", "Chuyên khoa đang có bác sĩ, không thể xóa.");

        _db.Specialties.Remove(entity);
        await _db.SaveChangesAsync();
    }
}