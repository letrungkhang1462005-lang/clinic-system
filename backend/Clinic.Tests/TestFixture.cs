using Clinic.Core.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests;

// Đồng hồ giả: cho phép test "đặt" thời điểm hiện tại
public class FakeClock : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeClock(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
}

public static class TestDates
{
    // "Bây giờ" của mọi test: thứ Năm 12/11/2026 08:00 giờ Việt Nam (= 01:00 UTC)
    public static readonly DateTimeOffset NowUtc = new(2026, 11, 12, 1, 0, 0, TimeSpan.Zero);
    public static readonly DateOnly Today = new(2026, 11, 12);   // thứ Năm
    public static readonly DateOnly Monday = new(2026, 11, 16);  // thứ Hai tuần sau
    public static readonly DateOnly Sunday = new(2026, 11, 15);
}

public sealed class TestFixture : IDisposable
{
    public ClinicDbContext Db { get; }
    public AppointmentService Appointments { get; }

    public int DoctorId { get; private set; }
    public int PatientAUserId { get; private set; }
    public int PatientAId { get; private set; }
    public int PatientBUserId { get; private set; }
    public int PatientBId { get; private set; }
    public int ReceptionistUserId { get; private set; }

    public TestFixture()
    {
        var options = new DbContextOptionsBuilder<ClinicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Db = new ClinicDbContext(options);
        Db.Database.EnsureCreated(); // áp dụng cả dữ liệu mẫu Roles, Specialties

        var clock = new FakeClock(TestDates.NowUtc);
        Appointments = new AppointmentService(Db, new ScheduleService(Db, clock), clock);

        Seed();
    }

    private static User NewUser(string email, string name, int roleId) =>
        new() { Email = email, FullName = name, PasswordHash = "x", RoleId = roleId };

    private void Seed()
    {
        // RoleId theo dữ liệu mẫu: 1 Admin, 2 Receptionist, 3 Doctor, 4 Patient
        var patientA = new Patient { User = NewUser("a@test.com", "Bệnh nhân A", 4) };
        var patientB = new Patient { User = NewUser("b@test.com", "Bệnh nhân B", 4) };
        var receptionist = NewUser("letan@test.com", "Lễ tân", 2);

        var doctor = new Doctor
        {
            User = NewUser("bs@test.com", "BS. Test", 3),
            SpecialtyId = 1,
            ConsultationFee = 200000
        };

        // Làm việc thứ Hai đến thứ Sáu, 07:30-11:30, mỗi slot 30 phút
        foreach (var day in new[]
                 {
                     DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
                     DayOfWeek.Thursday, DayOfWeek.Friday
                 })
        {
            doctor.Schedules.Add(new DoctorSchedule
            {
                DayOfWeek = day,
                StartTime = new TimeOnly(7, 30),
                EndTime = new TimeOnly(11, 30),
                SlotMinutes = 30
            });
        }

        Db.AddRange(patientA, patientB, receptionist, doctor);
        Db.SaveChanges();

        DoctorId = doctor.Id;
        PatientAId = patientA.Id;
        PatientAUserId = patientA.UserId;
        PatientBId = patientB.Id;
        PatientBUserId = patientB.UserId;
        ReceptionistUserId = receptionist.Id;
    }

    public void Dispose() => Db.Dispose();
}