using System.Globalization;
using Clinic.Core.Constants;

namespace Clinic.Web.Services;

public static class UiFormat
{
    // Tự định dạng thay vì dựa vào văn hóa vi-VN, vì Blazor WebAssembly mặc định không nạp đủ dữ liệu văn hóa
    public static string Money(decimal amount) =>
        amount.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.') + " đ";

    public static string RoleLabel(string? role) => role switch
    {
        RoleNames.Admin => "Quản trị viên",
        RoleNames.Receptionist => "Lễ tân",
        RoleNames.Doctor => "Bác sĩ",
        RoleNames.Patient => "Bệnh nhân",
        _ => ""
    };
}