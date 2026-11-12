using Clinic.Core.DTOs;
using Clinic.Core.Entities;

namespace Clinic.Core.Common;

public static class SlotGenerator
{
    /// <param name="schedules">Các ca làm việc của bác sĩ (hàm tự lọc theo thứ của ngày).</param>
    /// <param name="booked">Giờ bắt đầu của các lịch đã đặt (không tính lịch đã hủy).</param>
    /// <param name="date">Ngày cần xem.</param>
    /// <param name="nowLocal">Thời điểm hiện tại theo giờ Việt Nam.</param>
    public static List<SlotDto> Generate(
        IEnumerable<DoctorSchedule> schedules,
        ISet<TimeOnly> booked,
        DateOnly date,
        DateTime nowLocal)
    {
        var today = DateOnly.FromDateTime(nowLocal);
        var nowTime = TimeOnly.FromDateTime(nowLocal);
        var result = new Dictionary<TimeOnly, SlotDto>();

        foreach (var s in schedules.Where(x =>
                     x.IsActive && x.DayOfWeek == date.DayOfWeek && x.SlotMinutes > 0))
        {
            var startMin = s.StartTime.Hour * 60 + s.StartTime.Minute;
            var endMin = s.EndTime.Hour * 60 + s.EndTime.Minute;

            // Chỉ lấy slot nằm trọn trong ca: ca 7:30-11:30, slot 30 phút => 8 slot
            for (var m = startMin; m + s.SlotMinutes <= endMin; m += s.SlotMinutes)
            {
                var start = new TimeOnly(m / 60, m % 60);
                var endM = m + s.SlotMinutes;
                var end = new TimeOnly(endM / 60, endM % 60);

                if (result.ContainsKey(start)) continue; // phòng khi 2 ca chồng nhau

                var inPast = date < today || (date == today && start <= nowTime);
                result[start] = new SlotDto(start, end, !inPast && !booked.Contains(start));
            }
        }

        return result.Values.OrderBy(x => x.StartTime).ToList();
    }
}