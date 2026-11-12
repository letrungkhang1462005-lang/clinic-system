using Clinic.Core.Constants;
using Clinic.Core.Entities;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ClinicDbContext db)
    {
        await EnsureUserAsync(db, "admin@clinic.local", "Quản trị viên", "Admin@123", RoleNames.Admin);
        await EnsureUserAsync(db, "letan@clinic.local", "Lễ tân Demo", "Letan@123", RoleNames.Receptionist);

        await EnsureDoctorAsync(db, "bs.an@clinic.local", "BS. Nguyễn Văn An", 1, "Thạc sĩ, Bác sĩ", 10, 200000);
        await EnsureDoctorAsync(db, "bs.binh@clinic.local", "BS. Trần Thị Bình", 2, "Bác sĩ chuyên khoa I", 8, 250000);
        await EnsureDoctorAsync(db, "bs.cuong@clinic.local", "BS. Lê Minh Cường", 3, "Tiến sĩ, Bác sĩ", 15, 300000);
    }

    private static async Task EnsureUserAsync(
        ClinicDbContext db, string email, string fullName, string password, string roleName)
    {
        if (await db.Users.AnyAsync(u => u.Email == email)) return;

        var role = await db.Roles.FirstAsync(r => r.Name == roleName);
        db.Users.Add(new User
        {
            Email = email,
            FullName = fullName,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = role
        });
        await db.SaveChangesAsync();
    }

    private static async Task EnsureDoctorAsync(
        ClinicDbContext db, string email, string fullName,
        int specialtyId, string degree, int years, decimal fee)
    {
        if (await db.Users.AnyAsync(u => u.Email == email)) return;

        var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.Doctor);
        var doctor = new Doctor
        {
            User = new User
            {
                Email = email,
                FullName = fullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Doctor@123"),
                Role = role
            },
            SpecialtyId = specialtyId,
            Degree = degree,
            ExperienceYears = years,
            ConsultationFee = fee
        };

        var workDays = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday
        };

        foreach (var day in workDays)
        {
            doctor.Schedules.Add(new DoctorSchedule
            {
                DayOfWeek = day,
                StartTime = new TimeOnly(7, 30),
                EndTime = new TimeOnly(11, 30),
                SlotMinutes = 30
            });
            doctor.Schedules.Add(new DoctorSchedule
            {
                DayOfWeek = day,
                StartTime = new TimeOnly(13, 30),
                EndTime = new TimeOnly(16, 30),
                SlotMinutes = 30
            });
        }

        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();
    }
}