using Clinic.Core.Common;
using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Entities;
using Clinic.Core.Enums;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Clinic.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class AppointmentService : IAppointmentService
{
    private const int MinCancelHours = 2;
    private const int MaxDaysAhead = 90;

    private readonly ClinicDbContext _db;
    private readonly IScheduleService _schedules;
    private readonly TimeProvider _time;

    public AppointmentService(ClinicDbContext db, IScheduleService schedules, TimeProvider time)
    {
        _db = db;
        _schedules = schedules;
        _time = time;
    }

    // ---------- Đặt lịch ----------
    public async Task<AppointmentDto> CreateAsync(int userId, string role, CreateAppointmentRequest request)
    {
        var now = ClinicTime.Now(_time);
        var today = DateOnly.FromDateTime(now);

        if (!await _db.Doctors.AnyAsync(d => d.Id == request.DoctorId && d.User.IsActive))
            throw new AppException(404, "NOT_FOUND", "Không tìm thấy bác sĩ.");

        var patientId = await ResolvePatientIdAsync(userId, role, request.PatientId);

        if (request.Date > today.AddDays(MaxDaysAhead))
            throw new AppException(400, "TOO_FAR", $"Chỉ được đặt lịch trong vòng {MaxDaysAhead} ngày tới.");

        // Lớp bảo vệ 1: kiểm tra bằng code
        var slots = await _schedules.GetSlotsAsync(request.DoctorId, request.Date);
        var slot = slots.FirstOrDefault(s => s.StartTime == request.StartTime)
            ?? throw new AppException(400, "SLOT_NOT_FOUND", "Khung giờ không nằm trong lịch làm việc của bác sĩ.");

        if (request.Date.ToDateTime(slot.StartTime) <= now)
            throw new AppException(400, "PAST_TIME", "Không thể đặt lịch vào thời điểm đã qua.");

        if (!slot.IsAvailable)
            throw new AppException(409, "SLOT_TAKEN", "Khung giờ này đã có người đặt.");

        var patientBusy = await _db.Appointments.AnyAsync(a =>
            a.PatientId == patientId
            && a.AppointmentDate == request.Date
            && a.StartTime == slot.StartTime
            && a.Status != AppointmentStatus.Cancelled);
        if (patientBusy)
            throw new AppException(409, "PATIENT_BUSY", "Bệnh nhân đã có lịch khác vào giờ này.");

        var appointment = new Appointment
        {
            PatientId = patientId,
            DoctorId = request.DoctorId,
            AppointmentDate = request.Date,
            StartTime = slot.StartTime,
            EndTime = slot.EndTime,
            Reason = request.Reason,
            Status = role == RoleNames.Receptionist ? AppointmentStatus.Confirmed : AppointmentStatus.Pending
        };

        _db.Appointments.Add(appointment);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Lớp bảo vệ 2: hai request vượt qua kiểm tra cùng lúc, CSDL từ chối request đến sau
            throw new AppException(409, "SLOT_TAKEN", "Khung giờ này vừa có người đặt.");
        }

        return await GetDtoAsync(appointment.Id);
    }

    // ---------- Truy vấn ----------
    public async Task<List<AppointmentDto>> GetMyAsync(int userId, AppointmentStatus? status)
    {
        var query = Query().Where(a => a.Patient.UserId == userId);
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);

        var list = await query.OrderByDescending(a => a.AppointmentDate).ThenBy(a => a.StartTime).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<List<AppointmentDto>> GetListAsync(
        int userId, string role, DateOnly? date, int? doctorId, AppointmentStatus? status)
    {
        var query = Query();

        if (role == RoleNames.Doctor)
        {
            // Bác sĩ chỉ thấy lịch của chính mình
            var ownId = await _db.Doctors.Where(d => d.UserId == userId).Select(d => d.Id).FirstOrDefaultAsync();
            if (ownId == 0) throw new AppException(403, "FORBIDDEN", "Tài khoản chưa có hồ sơ bác sĩ.");
            query = query.Where(a => a.DoctorId == ownId);
        }
        else if (doctorId.HasValue)
        {
            query = query.Where(a => a.DoctorId == doctorId.Value);
        }

        if (date.HasValue) query = query.Where(a => a.AppointmentDate == date.Value);
        if (status.HasValue) query = query.Where(a => a.Status == status.Value);

        var list = await query.OrderBy(a => a.AppointmentDate).ThenBy(a => a.StartTime).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<AppointmentDto> GetByIdAsync(int id, int userId, string role)
    {
        var a = await Query().FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy lịch hẹn.");

        var allowed = role switch
        {
            RoleNames.Receptionist => true,
            RoleNames.Patient => a.Patient.UserId == userId,
            RoleNames.Doctor => a.Doctor.UserId == userId,
            _ => false
        };
        if (!allowed) throw new AppException(403, "FORBIDDEN", "Bạn không có quyền xem lịch hẹn này.");

        return ToDto(a);
    }

    // ---------- Đổi trạng thái ----------
    public async Task<AppointmentDto> ConfirmAsync(int id)
    {
        var a = await FindAsync(id);

        if (a.Status != AppointmentStatus.Pending)
            throw new AppException(409, "INVALID_STATUS", "Chỉ xác nhận được lịch đang ở trạng thái chờ.");

        if (a.AppointmentDate.ToDateTime(a.StartTime) <= ClinicTime.Now(_time))
            throw new AppException(400, "PAST_TIME", "Lịch hẹn đã qua giờ, không thể xác nhận.");

        a.Status = AppointmentStatus.Confirmed;
        await _db.SaveChangesAsync();
        return await GetDtoAsync(id);
    }

    public async Task<AppointmentDto> CheckInAsync(int id)
    {
        var a = await FindAsync(id);

        if (a.Status != AppointmentStatus.Confirmed)
            throw new AppException(409, "INVALID_STATUS", "Chỉ tiếp đón được lịch đã xác nhận.");

        var today = DateOnly.FromDateTime(ClinicTime.Now(_time));
        if (a.AppointmentDate != today)
            throw new AppException(400, "NOT_TODAY", "Chỉ tiếp đón lịch hẹn trong ngày hôm nay.");

        if (a.QueueNumber.HasValue)
            throw new AppException(409, "ALREADY_CHECKED_IN", "Lịch hẹn đã được tiếp đón.");

        var maxQueue = await _db.Appointments
            .Where(x => x.DoctorId == a.DoctorId && x.AppointmentDate == a.AppointmentDate && x.QueueNumber != null)
            .MaxAsync(x => (int?)x.QueueNumber) ?? 0;

        a.QueueNumber = maxQueue + 1;
        await _db.SaveChangesAsync();
        return await GetDtoAsync(id);
    }

    public async Task<AppointmentDto> CancelAsync(int id, int userId, string role, string? reason)
    {
        var a = await _db.Appointments.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy lịch hẹn.");

        if (role == RoleNames.Patient && a.Patient.UserId != userId)
            throw new AppException(403, "FORBIDDEN", "Bạn không thể hủy lịch của người khác.");

        if (a.Status is not (AppointmentStatus.Pending or AppointmentStatus.Confirmed))
            throw new AppException(409, "INVALID_STATUS", "Lịch hẹn không còn ở trạng thái có thể hủy.");

        if (role == RoleNames.Patient)
        {
            var untilStart = a.AppointmentDate.ToDateTime(a.StartTime) - ClinicTime.Now(_time);
            if (untilStart < TimeSpan.FromHours(MinCancelHours))
                throw new AppException(400, "TOO_LATE_TO_CANCEL",
                    $"Chỉ được hủy lịch trước giờ hẹn ít nhất {MinCancelHours} giờ. Vui lòng liên hệ lễ tân.");
        }

        a.Status = AppointmentStatus.Cancelled;
        a.CancelReason = reason;
        await _db.SaveChangesAsync();
        return await GetDtoAsync(id);
    }

    // ---------- Hàm hỗ trợ ----------
    private async Task<int> ResolvePatientIdAsync(int userId, string role, int? requestedPatientId)
    {
        if (role == RoleNames.Patient)
        {
            var id = await _db.Patients.Where(p => p.UserId == userId).Select(p => p.Id).FirstOrDefaultAsync();
            if (id == 0)
                throw new AppException(403, "NO_PATIENT_PROFILE", "Tài khoản chưa có hồ sơ bệnh nhân.");
            return id;
        }

        // Lễ tân đặt hộ: bắt buộc chỉ định bệnh nhân
        if (requestedPatientId is null || !await _db.Patients.AnyAsync(p => p.Id == requestedPatientId))
            throw new AppException(400, "PATIENT_NOT_FOUND", "Cần chỉ định một bệnh nhân hợp lệ.");

        return requestedPatientId.Value;
    }

    private async Task<Appointment> FindAsync(int id) =>
        await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id)
        ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy lịch hẹn.");

    private IQueryable<Appointment> Query() => _db.Appointments.AsNoTracking()
        .Include(a => a.Patient).ThenInclude(p => p.User)
        .Include(a => a.Doctor).ThenInclude(d => d.User)
        .Include(a => a.Doctor).ThenInclude(d => d.Specialty);

    private async Task<AppointmentDto> GetDtoAsync(int id) =>
        ToDto(await Query().FirstAsync(a => a.Id == id));

    private static AppointmentDto ToDto(Appointment a) => new(
        a.Id,
        a.PatientId, a.Patient.User.FullName,
        a.DoctorId, a.Doctor.User.FullName, a.Doctor.Specialty.Name,
        a.AppointmentDate, a.StartTime, a.EndTime,
        a.Status, a.Status.ToString(),
        a.QueueNumber, a.Reason, a.CancelReason, a.CreatedAt);

    // Mã lỗi SQL Server: 2601 = vi phạm unique index, 2627 = vi phạm unique constraint
    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}