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
}