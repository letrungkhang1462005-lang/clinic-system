using Clinic.Core.DTOs;
using Clinic.Core.Entities;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class MedicineService : IMedicineService
{
    private readonly ClinicDbContext _db;

    public MedicineService(ClinicDbContext db)
    {
        _db = db;
    }

    public async Task<List<MedicineDto>> GetListAsync(string? keyword, bool includeInactive)
    {
        var query = _db.Medicines.AsNoTracking().AsQueryable();

        if (!includeInactive)
            query = query.Where(m => m.IsActive);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(m => m.Name.Contains(k));
        }

        return await query.OrderBy(m => m.Name)
            .Select(m => new MedicineDto(m.Id, m.Name, m.Unit, m.Price, m.Description, m.IsActive))
            .ToListAsync();
    }

    public async Task<MedicineDto> CreateAsync(MedicineRequest request)
    {
        var name = request.Name.Trim();
        var unit = request.Unit.Trim();

        if (await _db.Medicines.AnyAsync(m => m.Name == name && m.Unit == unit))
            throw new AppException(409, "MEDICINE_EXISTS", "Thuốc này đã có trong danh mục.");

        var entity = new Medicine
        {
            Name = name,
            Unit = unit,
            Price = request.Price,
            Description = request.Description,
            IsActive = request.IsActive
        };
        _db.Medicines.Add(entity);
        await _db.SaveChangesAsync();

        return new MedicineDto(entity.Id, entity.Name, entity.Unit, entity.Price, entity.Description, entity.IsActive);
    }

    public async Task UpdateAsync(int id, MedicineRequest request)
    {
        var entity = await _db.Medicines.FindAsync(id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy thuốc.");

        var name = request.Name.Trim();
        var unit = request.Unit.Trim();

        if (await _db.Medicines.AnyAsync(m => m.Name == name && m.Unit == unit && m.Id != id))
            throw new AppException(409, "MEDICINE_EXISTS", "Thuốc này đã có trong danh mục.");

        entity.Name = name;
        entity.Unit = unit;
        entity.Price = request.Price;
        entity.Description = request.Description;
        entity.IsActive = request.IsActive; // ẩn thuốc bằng IsActive = false, không xóa cứng
        await _db.SaveChangesAsync();
    }
}