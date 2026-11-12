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

public class ExaminationService : IExaminationService
{
    private readonly ClinicDbContext _db;
    private readonly TimeProvider _time;

    public ExaminationService(ClinicDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    private DateOnly Today => DateOnly.FromDateTime(ClinicTime.Now(_time));

    // ---------- Bệnh án ----------
    public async Task<MedicalRecordDto> CreateRecordAsync(int doctorUserId, CreateMedicalRecordRequest request)
    {
        var doctorId = await GetDoctorIdAsync(doctorUserId);

        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == request.AppointmentId)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy lịch hẹn.");

        if (appt.DoctorId != doctorId)
            throw new AppException(403, "FORBIDDEN", "Bạn không phải bác sĩ của lịch hẹn này.");

        if (appt.Status != AppointmentStatus.Confirmed)
            throw new AppException(409, "INVALID_STATUS", "Chỉ ghi bệnh án cho lịch hẹn đã xác nhận.");

        if (appt.AppointmentDate > Today)
            throw new AppException(400, "NOT_YET", "Chưa đến ngày khám, không thể ghi bệnh án.");

        EnsureFollowUpValid(request.FollowUpDate);

        var record = new MedicalRecord
        {
            AppointmentId = appt.Id,
            Symptoms = request.Symptoms,
            Diagnosis = request.Diagnosis.Trim(),
            Notes = request.Notes,
            FollowUpDate = request.FollowUpDate,
            CreatedAt = _time.GetUtcNow().UtcDateTime
        };

        // Thêm bệnh án VÀ đổi trạng thái lịch hẹn trong cùng một SaveChanges:
        // hoặc cả hai thành công, hoặc cả hai không xảy ra.
        appt.Status = AppointmentStatus.Completed;
        _db.MedicalRecords.Add(record);

        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new AppException(409, "RECORD_EXISTS", "Lịch hẹn này đã có bệnh án.");
        }

        return await GetRecordDtoAsync(record.Id);
    }

    public async Task<MedicalRecordDto> UpdateRecordAsync(int id, int doctorUserId, UpdateMedicalRecordRequest request)
    {
        var doctorId = await GetDoctorIdAsync(doctorUserId);

        var record = await _db.MedicalRecords.Include(m => m.Appointment).FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy bệnh án.");

        if (record.Appointment.DoctorId != doctorId)
            throw new AppException(403, "FORBIDDEN", "Bạn không phải bác sĩ của bệnh án này.");

        EnsureNotLocked(record);
        EnsureFollowUpValid(request.FollowUpDate);

        record.Symptoms = request.Symptoms;
        record.Diagnosis = request.Diagnosis.Trim();
        record.Notes = request.Notes;
        record.FollowUpDate = request.FollowUpDate;
        await _db.SaveChangesAsync();

        return await GetRecordDtoAsync(id);
    }

    public async Task<MedicalRecordDto> GetRecordAsync(int id, int userId, string role)
    {
        var record = await RecordQuery().FirstOrDefaultAsync(m => m.Id == id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy bệnh án.");

        if (!CanView(record.Appointment, userId, role))
            throw new AppException(403, "FORBIDDEN", "Bạn không có quyền xem bệnh án này.");

        return ToDto(record);
    }

    // ---------- Đơn thuốc ----------
    public async Task<PrescriptionDto> CreatePrescriptionAsync(int doctorUserId, CreatePrescriptionRequest request)
    {
        var doctorId = await GetDoctorIdAsync(doctorUserId);

        var record = await _db.MedicalRecords
            .Include(m => m.Appointment)
            .Include(m => m.Prescription)
            .FirstOrDefaultAsync(m => m.Id == request.MedicalRecordId)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy bệnh án.");

        if (record.Appointment.DoctorId != doctorId)
            throw new AppException(403, "FORBIDDEN", "Bạn không phải bác sĩ của bệnh án này.");

        if (record.Prescription is not null)
            throw new AppException(409, "PRESCRIPTION_EXISTS", "Bệnh án này đã có đơn thuốc.");

        EnsureNotLocked(record);

        var ids = request.Details.Select(d => d.MedicineId).ToList();
        if (ids.Distinct().Count() != ids.Count)
            throw new AppException(400, "DUPLICATE_MEDICINE", "Đơn thuốc có thuốc bị lặp lại.");

        var activeCount = await _db.Medicines.CountAsync(m => ids.Contains(m.Id) && m.IsActive);
        if (activeCount != ids.Count)
            throw new AppException(400, "MEDICINE_NOT_FOUND", "Có thuốc không tồn tại hoặc đã ngừng sử dụng.");

        var prescription = new Prescription
        {
            MedicalRecordId = record.Id,
            Note = request.Note,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
            Details = request.Details.Select(d => new PrescriptionDetail
            {
                MedicineId = d.MedicineId,
                Quantity = d.Quantity,
                Dosage = d.Dosage.Trim(),
                DurationDays = d.DurationDays
            }).ToList()
        };

        _db.Prescriptions.Add(prescription);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            throw new AppException(409, "PRESCRIPTION_EXISTS", "Bệnh án này đã có đơn thuốc.");
        }

        var saved = await PrescriptionQuery().FirstAsync(p => p.Id == prescription.Id);
        return ToDto(saved);
    }

    public async Task<PrescriptionDto> GetPrescriptionAsync(int id, int userId, string role)
    {
        var prescription = await PrescriptionQuery().FirstOrDefaultAsync(p => p.Id == id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy đơn thuốc.");

        if (!CanView(prescription.MedicalRecord.Appointment, userId, role))
            throw new AppException(403, "FORBIDDEN", "Bạn không có quyền xem đơn thuốc này.");

        return ToDto(prescription);
    }

    // ---------- Lịch sử khám ----------
    public async Task<List<MedicalRecordDto>> GetHistoryAsync(int patientId, int userId, string role)
    {
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy bệnh nhân.");

        if (role == RoleNames.Patient)
        {
            if (patient.UserId != userId)
                throw new AppException(403, "FORBIDDEN", "Bạn chỉ được xem lịch sử khám của chính mình.");
        }
        else if (role == RoleNames.Doctor)
        {
            // Bác sĩ chỉ xem được bệnh nhân đã từng có lịch hẹn với mình
            var doctorId = await GetDoctorIdAsync(userId);
            var hasRelation = await _db.Appointments
                .AnyAsync(a => a.PatientId == patientId && a.DoctorId == doctorId);
            if (!hasRelation)
                throw new AppException(403, "FORBIDDEN", "Bệnh nhân này chưa từng khám với bạn.");
        }
        else
        {
            throw new AppException(403, "FORBIDDEN", "Bạn không có quyền xem lịch sử khám.");
        }

        var list = await RecordQuery()
            .Where(m => m.Appointment.PatientId == patientId)
            .OrderByDescending(m => m.Appointment.AppointmentDate)
            .ThenByDescending(m => m.Id)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    public async Task<List<MedicalRecordDto>> GetMyHistoryAsync(int userId)
    {
        var patientId = await _db.Patients.Where(p => p.UserId == userId).Select(p => p.Id).FirstOrDefaultAsync();
        if (patientId == 0)
            throw new AppException(403, "NO_PATIENT_PROFILE", "Tài khoản chưa có hồ sơ bệnh nhân.");

        return await GetHistoryAsync(patientId, userId, RoleNames.Patient);
    }

    // ---------- Hàm hỗ trợ ----------
    private async Task<int> GetDoctorIdAsync(int userId)
    {
        var id = await _db.Doctors.Where(d => d.UserId == userId).Select(d => d.Id).FirstOrDefaultAsync();
        if (id == 0)
            throw new AppException(403, "FORBIDDEN", "Tài khoản chưa có hồ sơ bác sĩ.");
        return id;
    }

    private void EnsureFollowUpValid(DateOnly? followUp)
    {
        if (followUp is { } date && date <= Today)
            throw new AppException(400, "INVALID_FOLLOW_UP", "Ngày tái khám phải sau hôm nay.");
    }

    // Bệnh án chỉ được sửa trong đúng ngày tạo (theo giờ Việt Nam)
    private void EnsureNotLocked(MedicalRecord record)
    {
        var createdDay = DateOnly.FromDateTime(record.CreatedAt.AddHours(7));
        if (createdDay != Today)
            throw new AppException(409, "RECORD_LOCKED", "Bệnh án đã khóa, chỉ được sửa trong ngày khám.");
    }

    private static bool CanView(Appointment a, int userId, string role) => role switch
    {
        RoleNames.Doctor => a.Doctor.UserId == userId,
        RoleNames.Patient => a.Patient.UserId == userId,
        _ => false
    };

    private IQueryable<MedicalRecord> RecordQuery() => _db.MedicalRecords.AsNoTracking()
        .Include(m => m.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
        .Include(m => m.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User)
        .Include(m => m.Prescription).ThenInclude(p => p!.Details).ThenInclude(d => d.Medicine);

    private IQueryable<Prescription> PrescriptionQuery() => _db.Prescriptions.AsNoTracking()
        .Include(p => p.Details).ThenInclude(d => d.Medicine)
        .Include(p => p.MedicalRecord).ThenInclude(m => m.Appointment).ThenInclude(a => a.Patient)
        .Include(p => p.MedicalRecord).ThenInclude(m => m.Appointment).ThenInclude(a => a.Doctor);

    private async Task<MedicalRecordDto> GetRecordDtoAsync(int id) =>
        ToDto(await RecordQuery().FirstAsync(m => m.Id == id));

    private static MedicalRecordDto ToDto(MedicalRecord m) => new(
        m.Id, m.AppointmentId, m.Appointment.AppointmentDate,
        m.Appointment.PatientId, m.Appointment.Patient.User.FullName,
        m.Appointment.DoctorId, m.Appointment.Doctor.User.FullName,
        m.Symptoms, m.Diagnosis, m.Notes, m.FollowUpDate, m.CreatedAt,
        m.Prescription is null ? null : ToDto(m.Prescription));

    private static PrescriptionDto ToDto(Prescription p) => new(
        p.Id, p.MedicalRecordId, p.Note, p.CreatedAt,
        p.Details.OrderBy(d => d.Id)
            .Select(d => new PrescriptionItemDto(
                d.Id, d.MedicineId, d.Medicine.Name, d.Medicine.Unit,
                d.Quantity, d.Dosage, d.DurationDays))
            .ToList());

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is SqlException { Number: 2601 or 2627 };
}