using Clinic.Core.Common;
using Clinic.Core.DTOs;
using Clinic.Core.Entities;
using Clinic.Core.Enums;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class ScheduleService : IScheduleService
{
    private readonly ClinicDbContext _db;
    private readonly TimeProvider _time;

    public ScheduleService(ClinicDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    private static ScheduleDto ToDto(DoctorSchedule s) =>
        new(s.Id, s.DoctorId, s.DayOfWeek, s.StartTime, s.EndTime, s.SlotMinutes, s.IsActive);

    public async Task<List<ScheduleDto>> GetByDoctorAsync(int doctorId)
    {
        await EnsureDoctorExistsAsync(doctorId);

        var list = await _db.DoctorSchedules.AsNoTracking()
            .Where(s => s.DoctorId == doctorId)
            .OrderBy(s => s.DayOfWeek).ThenBy(s => s.StartTime)
            .ToListAsync();

        return list.Select(ToDto).ToList();
    }

    public async Task<ScheduleDto> AddAsync(int doctorId, ScheduleRequest request)
    {
        await EnsureDoctorExistsAsync(doctorId);
        Validate(request);
        await EnsureNoOverlapAsync(doctorId, request, excludeId: 0);

        var entity = new DoctorSchedule
        {
            DoctorId = doctorId,
            DayOfWeek = request.DayOfWeek,
            StartTime = request.StartTime,
            EndTime = request.EndTime,
            SlotMinutes = request.SlotMinutes
        };
        _db.DoctorSchedules.Add(entity);
        await _db.SaveChangesAsync();
        return ToDto(entity);
    }

    public async Task UpdateAsync(int scheduleId, ScheduleRequest request)
    {
        var entity = await _db.DoctorSchedules.FindAsync(scheduleId)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy ca làm việc.");

        Validate(request);
        await EnsureNoOverlapAsync(entity.DoctorId, request, excludeId: scheduleId);

        entity.DayOfWeek = request.DayOfWeek;
        entity.StartTime = request.StartTime;
        entity.EndTime = request.EndTime;
        entity.SlotMinutes = request.SlotMinutes;
        await _db.SaveChangesAsync();
    }

    public async Task DeleteAsync(int scheduleId)
    {
        var entity = await _db.DoctorSchedules.FindAsync(scheduleId)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy ca làm việc.");

        _db.DoctorSchedules.Remove(entity);
        await _db.SaveChangesAsync();
    }

    public async Task<List<SlotDto>> GetSlotsAsync(int doctorId, DateOnly date)
    {
        await EnsureDoctorExistsAsync(doctorId);

        var dow = date.DayOfWeek;
        var schedules = await _db.DoctorSchedules.AsNoTracking()
            .Where(s => s.DoctorId == doctorId && s.IsActive && s.DayOfWeek == dow)
            .ToListAsync();

        var bookedList = await _db.Appointments.AsNoTracking()
            .Where(a => a.DoctorId == doctorId
                        && a.AppointmentDate == date
                        && a.Status != AppointmentStatus.Cancelled)
            .Select(a => a.StartTime)
            .ToListAsync();

        return SlotGenerator.Generate(schedules, bookedList.ToHashSet(), date, ClinicTime.Now(_time));
    }

    // ---------- Hàm hỗ trợ ----------

    private async Task EnsureDoctorExistsAsync(int doctorId)
    {
        if (!await _db.Doctors.AnyAsync(d => d.Id == doctorId && d.User.IsActive))
            throw new AppException(404, "NOT_FOUND", "Không tìm thấy bác sĩ.");
    }

    private static void Validate(ScheduleRequest r)
    {
        if (r.EndTime <= r.StartTime)
            throw new AppException(400, "INVALID_TIME_RANGE", "Giờ kết thúc phải sau giờ bắt đầu.");

        if ((r.EndTime - r.StartTime).TotalMinutes < r.SlotMinutes)
            throw new AppException(400, "INVALID_SLOT", "Ca làm việc ngắn hơn một khung giờ khám.");
    }

    // Hai ca chồng nhau khi: ca cũ bắt đầu trước khi ca mới kết thúc VÀ ca mới bắt đầu trước khi ca cũ kết thúc
    private async Task EnsureNoOverlapAsync(int doctorId, ScheduleRequest r, int excludeId)
    {
        var overlap = await _db.DoctorSchedules.AnyAsync(s =>
            s.DoctorId == doctorId
            && s.IsActive
            && s.Id != excludeId
            && s.DayOfWeek == r.DayOfWeek
            && s.StartTime < r.EndTime
            && r.StartTime < s.EndTime);

        if (overlap)
            throw new AppException(409, "SCHEDULE_OVERLAP", "Ca làm việc bị chồng lên ca khác trong cùng ngày.");
    }
}