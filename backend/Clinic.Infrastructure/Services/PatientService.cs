using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Entities;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class PatientService : IPatientService
{
    // Email giả của bệnh nhân walk-in. Họ không có tài khoản đăng nhập.
    public const string WalkInEmailSuffix = "@walkin.clinic.local";

    private readonly ClinicDbContext _db;
    private readonly TimeProvider _time;

    public PatientService(ClinicDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<PatientDto> CreateWalkInAsync(CreatePatientRequest request)
    {
        var phone = request.Phone.Trim();

        if (await _db.Patients.AnyAsync(p => p.User.Phone == phone))
            throw new AppException(409, "PATIENT_EXISTS", "Số điện thoại này đã có hồ sơ bệnh nhân.");

        var role = await _db.Roles.FirstAsync(r => r.Name == RoleNames.Patient);

        var patient = new Patient
        {
            User = new User
            {
                Email = $"walkin-{Guid.NewGuid():N}{WalkInEmailSuffix}",
                FullName = request.FullName.Trim(),
                Phone = phone,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString()),
                Role = role,
                IsActive = false, // không đăng nhập được
                CreatedAt = _time.GetUtcNow().UtcDateTime
            },
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Address = request.Address
        };

        _db.Patients.Add(patient);
        await _db.SaveChangesAsync();
        return await GetDtoAsync(patient.Id);
    }

    public async Task<List<PatientDto>> GetListAsync(int userId, string role, string? keyword)
    {
        var query = _db.Patients.AsNoTracking().Include(p => p.User).AsQueryable();

        if (role == RoleNames.Doctor)
        {
            // Bác sĩ chỉ thấy bệnh nhân đã từng có lịch hẹn với mình
            var ids = await VisitedPatientIdsAsync(userId);
            query = query.Where(p => ids.Contains(p.Id));
        }

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var k = keyword.Trim();
            query = query.Where(p => p.User.FullName.Contains(k)
                                     || (p.User.Phone != null && p.User.Phone.Contains(k)));
        }

        var list = await query.OrderBy(p => p.User.FullName).Take(50).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<PatientDto> GetByIdAsync(int id, int userId, string role)
    {
        var patient = await _db.Patients.AsNoTracking().Include(p => p.User)
            .FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy bệnh nhân.");

        if (role == RoleNames.Doctor && !(await VisitedPatientIdsAsync(userId)).Contains(id))
            throw new AppException(403, "FORBIDDEN", "Bệnh nhân này chưa từng khám với bạn.");

        return ToDto(patient);
    }

    private async Task<List<int>> VisitedPatientIdsAsync(int doctorUserId)
    {
        var doctorId = await _db.Doctors.Where(d => d.UserId == doctorUserId).Select(d => d.Id).FirstOrDefaultAsync();
        if (doctorId == 0)
            throw new AppException(403, "FORBIDDEN", "Tài khoản chưa có hồ sơ bác sĩ.");

        return await _db.Appointments.Where(a => a.DoctorId == doctorId)
            .Select(a => a.PatientId).Distinct().ToListAsync();
    }

    private async Task<PatientDto> GetDtoAsync(int id) =>
        ToDto(await _db.Patients.AsNoTracking().Include(p => p.User).FirstAsync(p => p.Id == id));

    private static PatientDto ToDto(Patient p)
    {
        var hasAccount = !p.User.Email.EndsWith(WalkInEmailSuffix);
        return new PatientDto(
            p.Id, p.User.FullName, p.User.Phone,
            hasAccount ? p.User.Email : null, // không lộ email giả
            p.DateOfBirth, p.Gender, p.Address, hasAccount);
    }
}