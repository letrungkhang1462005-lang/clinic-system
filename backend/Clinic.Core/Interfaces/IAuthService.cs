using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Clinic.Core.DTOs;
using Clinic.Core.Entities;

namespace Clinic.Core.Interfaces;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) Generate(User user);
}

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(RegisterRequest request);
    Task<AuthResponse> LoginAsync(LoginRequest request);
    Task<UserInfo> GetMeAsync(int userId);
}
