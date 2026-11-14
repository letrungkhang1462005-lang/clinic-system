using Clinic.Core.Common;
using Clinic.Core.DTOs;
using Clinic.Core.Enums;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class DashboardService : IDashboardService
{
    private readonly ClinicDbContext _db;
    private readonly TimeProvider _time;

    public DashboardService(ClinicDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<DashboardSummaryDto> GetSummaryAsync(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);
        var (fromUtc, toUtc) = UtcBounds(start, end);

        // Doanh thu = hóa đơn ĐÃ THU, tính theo ngày thu tiền
        var paid = _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAt >= fromUtc && i.PaidAt < toUtc);

        var totalRevenue = await paid.SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
        var paidCount = await paid.CountAsync();

        var unpaidAmount = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Unpaid && i.CreatedAt >= fromUtc && i.CreatedAt < toUtc)
            .SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;

        var byStatus = await _db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate >= start && a.AppointmentDate <= end)
            .GroupBy(a => a.Status)
            .Select(g => new { Status = g.Key, Count = g.Count() })
            .ToListAsync();

        var total = byStatus.Sum(x => x.Count);
        var completed = byStatus.Where(x => x.Status == AppointmentStatus.Completed).Sum(x => x.Count);
        var cancelled = byStatus.Where(x => x.Status == AppointmentStatus.Cancelled).Sum(x => x.Count);
        var cancelRate = total == 0 ? 0 : Math.Round(cancelled * 100.0 / total, 1);

        var newPatients = await _db.Patients.AsNoTracking()
            .CountAsync(p => p.User.CreatedAt >= fromUtc && p.User.CreatedAt < toUtc);

        return new DashboardSummaryDto(
            start, end, totalRevenue, paidCount, unpaidAmount,
            total, completed, cancelled, cancelRate, newPatients);
    }

    public async Task<List<RevenuePointDto>> GetRevenueByDayAsync(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);
        var (fromUtc, toUtc) = UtcBounds(start, end);

        var rows = await _db.Invoices.AsNoTracking()
            .Where(i => i.Status == InvoiceStatus.Paid && i.PaidAt >= fromUtc && i.PaidAt < toUtc)
            .Select(i => new { PaidAt = i.PaidAt!.Value, i.TotalAmount })
            .ToListAsync();

        // Gom theo ngày giờ Việt Nam trong bộ nhớ (dữ liệu đã được lọc theo khoảng ngày)
        var byDay = rows
            .GroupBy(r => DateOnly.FromDateTime(r.PaidAt.AddHours(7)))
            .ToDictionary(g => g.Key, g => (Revenue: g.Sum(x => x.TotalAmount), Count: g.Count()));

        // Điền cả những ngày không có doanh thu để biểu đồ liền mạch
        var result = new List<RevenuePointDto>();
        for (var day = start; day <= end; day = day.AddDays(1))
        {
            byDay.TryGetValue(day, out var v);
            result.Add(new RevenuePointDto(day, v.Revenue, v.Count));
        }
        return result;
    }

    public async Task<List<TopDoctorDto>> GetTopDoctorsAsync(DateOnly? from, DateOnly? to, int limit)
    {
        var (start, end) = ResolveRange(from, to);
        limit = Math.Clamp(limit, 1, 20);

        var stats = await _db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate >= start && a.AppointmentDate <= end
                        && a.Status != AppointmentStatus.Cancelled)
            .GroupBy(a => a.DoctorId)
            .Select(g => new
            {
                DoctorId = g.Key,
                Total = g.Count(),
                Completed = g.Count(a => a.Status == AppointmentStatus.Completed)
            })
            .OrderByDescending(x => x.Total).ThenBy(x => x.DoctorId)
            .Take(limit)
            .ToListAsync();

        var ids = stats.Select(s => s.DoctorId).ToList();
        var doctors = await _db.Doctors.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new { d.Id, Name = d.User.FullName, Specialty = d.Specialty.Name })
            .ToDictionaryAsync(d => d.Id);

        return stats.Select(s => new TopDoctorDto(
            s.DoctorId, doctors[s.DoctorId].Name, doctors[s.DoctorId].Specialty, s.Total, s.Completed)).ToList();
    }

    public async Task<List<PeakHourDto>> GetPeakHoursAsync(DateOnly? from, DateOnly? to)
    {
        var (start, end) = ResolveRange(from, to);

        var times = await _db.Appointments.AsNoTracking()
            .Where(a => a.AppointmentDate >= start && a.AppointmentDate <= end
                        && a.Status != AppointmentStatus.Cancelled)
            .Select(a => a.StartTime)
            .ToListAsync();

        return times.GroupBy(t => t.Hour)
            .OrderBy(g => g.Key)
            .Select(g => new PeakHourDto(g.Key, g.Count()))
            .ToList();
    }

    // ---------- Hàm hỗ trợ ----------
    // Mặc định 30 ngày gần nhất, tối đa 366 ngày
    private (DateOnly Start, DateOnly End) ResolveRange(DateOnly? from, DateOnly? to)
    {
        var today = DateOnly.FromDateTime(ClinicTime.Now(_time));
        var end = to ?? today;
        var start = from ?? end.AddDays(-29);

        if (start > end)
            throw new AppException(400, "INVALID_RANGE", "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
        if (end.DayNumber - start.DayNumber > 365)
            throw new AppException(400, "INVALID_RANGE", "Khoảng thời gian tối đa là 366 ngày.");

        return (start, end);
    }

    // Ngày theo giờ Việt Nam -> mốc UTC [từ, đến) để so với cột lưu UTC
    private static (DateTime FromUtc, DateTime ToUtc) UtcBounds(DateOnly start, DateOnly end) => (
        start.ToDateTime(TimeOnly.MinValue).AddHours(-7),
        end.AddDays(1).ToDateTime(TimeOnly.MinValue).AddHours(-7));
}