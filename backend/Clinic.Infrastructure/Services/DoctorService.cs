using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Entities;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class DoctorService : IDoctorService
{
    private readonly ClinicDbContext _db;

    public DoctorService(ClinicDbContext db)
    {
        _db = db;
    }

    private static IQueryable<DoctorDto> Project(IQueryable<Doctor> query) =>
        query.Select(d => new DoctorDto(
            d.Id, d.User.FullName, d.User.Email, d.User.Phone,
            d.SpecialtyId, d.Specialty.Name,
            d.Degree, d.ExperienceYears, d.Bio,
            d.ConsultationFee, d.User.IsActive));

    public async Task<List<DoctorDto>> GetListAsync(int? specialtyId, string? keyword)
    {
        var query = _db.Doctors.AsNoTracking().Where(d => d.User.IsActive);

        if (specialtyId.HasValue)
            query = query.Where(d => d.SpecialtyId == specialtyId.Value);

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(d => d.User.FullName.Contains(k));
        }

        return await Project(query.OrderBy(d => d.User.FullName)).ToListAsync();
    }

    public async Task<DoctorDto> GetByIdAsync(int id) =>
        await Project(_db.Doctors.AsNoTracking().Where(d => d.Id == id && d.User.IsActive))
            .FirstOrDefaultAsync()
        ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy bác sĩ.");

    public async Task<DoctorDto> CreateAsync(CreateDoctorRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new AppException(409, "EMAIL_EXISTS", "Email đã được sử dụng.");

        if (!await _db.Specialties.AnyAsync(s => s.Id == request.SpecialtyId))
            throw new AppException(400, "SPECIALTY_NOT_FOUND", "Chuyên khoa không tồn tại.");

        var role = await _db.Roles.FirstAsync(r => r.Name == RoleNames.Doctor);

        var doctor = new Doctor
        {
            User = new User
            {
                Email = email,
                FullName = request.FullName.Trim(),
                Phone = request.Phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
                Role = role
            },
            SpecialtyId = request.SpecialtyId,
            Degree = request.Degree,
            ExperienceYears = request.ExperienceYears,
            Bio = request.Bio,
            ConsultationFee = request.ConsultationFee
        };

        _db.Doctors.Add(doctor);
        await _db.SaveChangesAsync();
        return await GetByIdAsync(doctor.Id);
    }

    public async Task UpdateAsync(int id, UpdateDoctorRequest request)
    {
        var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy bác sĩ.");

        if (!await _db.Specialties.AnyAsync(s => s.Id == request.SpecialtyId))
            throw new AppException(400, "SPECIALTY_NOT_FOUND", "Chuyên khoa không tồn tại.");

        doctor.User.FullName = request.FullName.Trim();
        doctor.User.Phone = request.Phone;
        doctor.User.IsActive = request.IsActive;
        doctor.SpecialtyId = request.SpecialtyId;
        doctor.Degree = request.Degree;
        doctor.ExperienceYears = request.ExperienceYears;
        doctor.Bio = request.Bio;
        doctor.ConsultationFee = request.ConsultationFee;

        await _db.SaveChangesAsync();
    }

    // Không xóa cứng: bác sĩ đã có lịch hẹn và bệnh án, xóa sẽ làm hỏng dữ liệu cũ.
    public async Task DeactivateAsync(int id)
    {
        var doctor = await _db.Doctors.Include(d => d.User).FirstOrDefaultAsync(d => d.Id == id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy bác sĩ.");

        doctor.User.IsActive = false;
        await _db.SaveChangesAsync();
    }
}