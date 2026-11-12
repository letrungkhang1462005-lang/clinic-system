namespace Clinic.Core.Common;

public static class ClinicTime
{
    // Giờ Việt Nam = UTC + 7 (không có giờ mùa hè)
    public static DateTime Now(TimeProvider time)
        => time.GetUtcNow().UtcDateTime.AddHours(7);
}