using Clinic.Core.Entities;
using Clinic.Infrastructure.Data;
using Clinic.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Tests;

// Đồng hồ giả: cho phép test "đặt" và "tua" thời gian
public class FakeClock : TimeProvider
{
    private DateTimeOffset _utcNow;

    public FakeClock(DateTimeOffset utcNow)
    {
        _utcNow = utcNow;
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;

    public void Advance(TimeSpan span) => _utcNow += span;
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
    public FakeClock Clock { get; }
    public AppointmentService Appointments { get; }
    public ExaminationService Examinations { get; }

    public int DoctorId { get; private set; }
    public int DoctorUserId { get; private set; }
    public int Doctor2UserId { get; private set; }
    public int PatientAUserId { get; private set; }
    public int PatientAId { get; private set; }
    public int PatientBUserId { get; private set; }
    public int PatientBId { get; private set; }
    public int ReceptionistUserId { get; private set; }
    public int Medicine1Id { get; private set; }
    public int Medicine2Id { get; private set; }
    public int InactiveMedicineId { get; private set; }

    public TestFixture()
    {
        var options = new DbContextOptionsBuilder<ClinicDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        Db = new ClinicDbContext(options);
        Db.Database.EnsureCreated(); // áp dụng cả dữ liệu mẫu Roles, Specialties

        Clock = new FakeClock(TestDates.NowUtc);
        Appointments = new AppointmentService(Db, new ScheduleService(Db, Clock), Clock);
        Examinations = new ExaminationService(Db, Clock);

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

        // Bác sĩ thứ hai: dùng để kiểm tra "bác sĩ khác không được động vào"
        var doctor2 = new Doctor
        {
            User = NewUser("bs2@test.com", "BS. Khác", 3),
            SpecialtyId = 2,
            ConsultationFee = 250000
        };

        var med1 = new Medicine { Name = "Paracetamol 500mg", Unit = "viên", Price = 1500 };
        var med2 = new Medicine { Name = "Cetirizine 10mg", Unit = "viên", Price = 2000 };
        var medOff = new Medicine { Name = "Thuốc ngừng bán", Unit = "viên", Price = 1000, IsActive = false };

        Db.AddRange(patientA, patientB, receptionist, doctor, doctor2, med1, med2, medOff);
        Db.SaveChanges();

        DoctorId = doctor.Id;
        DoctorUserId = doctor.UserId;
        Doctor2UserId = doctor2.UserId;
        PatientAId = patientA.Id;
        PatientAUserId = patientA.UserId;
        PatientBId = patientB.Id;
        PatientBUserId = patientB.UserId;
        ReceptionistUserId = receptionist.Id;
        Medicine1Id = med1.Id;
        Medicine2Id = med2.Id;
        InactiveMedicineId = medOff.Id;
    }

    public void Dispose() => Db.Dispose();
}