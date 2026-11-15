using System.Globalization;
using Clinic.Core.Constants;
using Clinic.Core.Enums;
using MudBlazor;

namespace Clinic.Web.Services;

public static class UiFormat
{
    // Tự định dạng thay vì dựa vào văn hóa vi-VN, vì Blazor WebAssembly mặc định không nạp đủ dữ liệu văn hóa
    public static string Money(decimal amount) =>
        amount.ToString("N0", CultureInfo.InvariantCulture).Replace(',', '.') + " đ";

    public static string Date(DateOnly date) => date.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    public static string Time(TimeOnly time) => time.ToString("HH:mm", CultureInfo.InvariantCulture);

    // API lưu thời điểm theo UTC, giao diện hiển thị theo giờ Việt Nam (UTC+7)
    public static string DateTimeVn(DateTime utc) =>
        DateTime.SpecifyKind(utc, DateTimeKind.Utc).AddHours(7)
            .ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string RoleLabel(string? role) => role switch
    {
        RoleNames.Admin => "Quản trị viên",
        RoleNames.Receptionist => "Lễ tân",
        RoleNames.Doctor => "Bác sĩ",
        RoleNames.Patient => "Bệnh nhân",
        _ => ""
    };

    public static string StatusLabel(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => "Chờ xác nhận",
        AppointmentStatus.Confirmed => "Đã xác nhận",
        AppointmentStatus.Completed => "Đã khám",
        AppointmentStatus.Cancelled => "Đã hủy",
        _ => status.ToString()
    };

    public static Color StatusColor(AppointmentStatus status) => status switch
    {
        AppointmentStatus.Pending => Color.Warning,
        AppointmentStatus.Confirmed => Color.Info,
        AppointmentStatus.Completed => Color.Success,
        AppointmentStatus.Cancelled => Color.Default,
        _ => Color.Default
    };

    public static string InvoiceStatusLabel(InvoiceStatus status) =>
        status == InvoiceStatus.Paid ? "Đã thanh toán" : "Chưa thanh toán";

    public static Color InvoiceStatusColor(InvoiceStatus status) =>
        status == InvoiceStatus.Paid ? Color.Success : Color.Warning;

    public static string PaymentLabel(string? method) => method switch
    {
        "Cash" => "Tiền mặt",
        "Transfer" => "Chuyển khoản",
        _ => method ?? ""
    };

    public static string ItemTypeLabel(InvoiceItemType type) => type switch
    {
        InvoiceItemType.Examination => "Khám bệnh",
        InvoiceItemType.Service => "Dịch vụ",
        InvoiceItemType.Medicine => "Thuốc",
        _ => type.ToString()
    };
}