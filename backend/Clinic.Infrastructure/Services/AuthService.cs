using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Entities;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Clinic.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class AuthService : IAuthService
{
    private readonly ClinicDbContext _db;
    private readonly IJwtTokenService _jwt;

    public AuthService(ClinicDbContext db, IJwtTokenService jwt)
    {
        _db = db;
        _jwt = jwt;
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        if (await _db.Users.AnyAsync(u => u.Email == email))
            throw new AppException(409, "EMAIL_EXISTS", "Email đã được sử dụng.");

        var role = await _db.Roles.FirstAsync(r => r.Name == RoleNames.Patient);

        var user = new User
        {
            Email = email,
            FullName = request.FullName.Trim(),
            Phone = request.Phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = role
        };

        _db.Patients.Add(new Patient
        {
            User = user,
            DateOfBirth = request.DateOfBirth,
            Gender = request.Gender,
            Address = request.Address
        });

        await _db.SaveChangesAsync();
        return BuildResponse(user);
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest request)
    {
        var email = request.Email.Trim().ToLowerInvariant();

        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Email == email);

        // Cùng một thông báo cho mọi trường hợp sai để không lộ email nào tồn tại.
        if (user is null || !user.IsActive ||
            !BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
        {
            throw new AppException(401, "INVALID_CREDENTIALS", "Email hoặc mật khẩu không đúng.");
        }

        return BuildResponse(user);
    }

    public async Task<UserInfo> GetMeAsync(int userId)
    {
        var user = await _db.Users.Include(u => u.Role)
            .FirstOrDefaultAsync(u => u.Id == userId)
            ?? throw new AppException(404, "USER_NOT_FOUND", "Không tìm thấy người dùng.");

        return ToInfo(user);
    }

    private AuthResponse BuildResponse(User user)
    {
        var (token, expiresAt) = _jwt.Generate(user);
        return new AuthResponse { Token = token, ExpiresAt = expiresAt, User = ToInfo(user) };
    }

    private static UserInfo ToInfo(User user) => new()
    {
        Id = user.Id,
        Email = user.Email,
        FullName = user.FullName,
        Role = user.Role.Name
    };
}