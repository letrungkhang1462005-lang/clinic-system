using Clinic.Core.Common;
using Clinic.Core.Entities;
using Xunit;

namespace Clinic.Tests;

public class SlotGeneratorTests
{
    private static readonly DateTime Now = new(2026, 11, 12, 8, 0, 0);
    private static readonly HashSet<TimeOnly> NoBooking = new();

    private static DoctorSchedule Shift(DayOfWeek day, int sh, int sm, int eh, int em, int slot = 30) => new()
    {
        DayOfWeek = day,
        StartTime = new TimeOnly(sh, sm),
        EndTime = new TimeOnly(eh, em),
        SlotMinutes = slot,
        IsActive = true
    };

    [Fact]
    public void Morning_shift_of_4_hours_gives_8_slots()
    {
        var slots = SlotGenerator.Generate(
            new[] { Shift(DayOfWeek.Monday, 7, 30, 11, 30) }, NoBooking, TestDates.Monday, Now);

        Assert.Equal(8, slots.Count);
        Assert.Equal(new TimeOnly(7, 30), slots.First().StartTime);
        Assert.Equal(new TimeOnly(11, 30), slots.Last().EndTime);
        Assert.All(slots, s => Assert.True(s.IsAvailable));
    }

    [Fact]
    public void Shift_that_does_not_divide_evenly_drops_the_partial_slot()
    {
        // 07:30-11:45 chia 30 phút: slot 11:30-12:00 vượt giờ kết thúc nên bị bỏ
        var slots = SlotGenerator.Generate(
            new[] { Shift(DayOfWeek.Monday, 7, 30, 11, 45) }, NoBooking, TestDates.Monday, Now);

        Assert.Equal(8, slots.Count);
    }

    [Fact]
    public void Booked_slot_is_marked_unavailable()
    {
        var booked = new HashSet<TimeOnly> { new(8, 0) };

        var slots = SlotGenerator.Generate(
            new[] { Shift(DayOfWeek.Monday, 7, 30, 11, 30) }, booked, TestDates.Monday, Now);

        Assert.False(slots.Single(s => s.StartTime == new TimeOnly(8, 0)).IsAvailable);
        Assert.Equal(7, slots.Count(s => s.IsAvailable));
    }

    [Fact]
    public void Slots_that_already_started_today_are_unavailable()
    {
        // Bây giờ 09:00 (giờ VN): các slot 07:30, 08:00, 08:30, 09:00 đã qua
        var nineAm = new DateTime(2026, 11, 12, 9, 0, 0);

        var slots = SlotGenerator.Generate(
            new[] { Shift(DayOfWeek.Thursday, 7, 30, 11, 30) }, NoBooking, TestDates.Today, nineAm);

        Assert.Equal(8, slots.Count);
        Assert.Equal(4, slots.Count(s => s.IsAvailable));
        Assert.Equal(new TimeOnly(9, 30), slots.First(s => s.IsAvailable).StartTime);
    }

    [Fact]
    public void Day_without_a_shift_has_no_slots()
    {
        var slots = SlotGenerator.Generate(
            new[] { Shift(DayOfWeek.Monday, 7, 30, 11, 30) }, NoBooking, TestDates.Sunday, Now);

        Assert.Empty(slots);
    }
}