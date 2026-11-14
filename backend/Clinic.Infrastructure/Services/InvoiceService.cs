using Clinic.Core.Common;
using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Entities;
using Clinic.Core.Enums;
using Clinic.Core.Exceptions;
using Clinic.Core.Interfaces;
using Clinic.Infrastructure.Data;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Services;

public class InvoiceService : IInvoiceService
{
    private static readonly string[] PaymentMethods = { "Cash", "Transfer" };

    private readonly ClinicDbContext _db;
    private readonly TimeProvider _time;

    public InvoiceService(ClinicDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public async Task<InvoiceDto> CreateAsync(int receptionistUserId, CreateInvoiceRequest request)
    {
        var appt = await _db.Appointments
            .Include(a => a.Doctor)
            .Include(a => a.MedicalRecord).ThenInclude(m => m!.Prescription)
                .ThenInclude(p => p!.Details).ThenInclude(d => d.Medicine)
            .FirstOrDefaultAsync(a => a.Id == request.AppointmentId)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy lịch hẹn.");

        if (appt.Status != AppointmentStatus.Completed)
            throw new AppException(409, "INVALID_STATUS", "Chỉ lập hóa đơn cho lượt khám đã hoàn tất.");

        if (await _db.Invoices.AnyAsync(i => i.AppointmentId == appt.Id))
            throw new AppException(409, "INVOICE_EXISTS", "Lượt khám này đã có hóa đơn.");

        var serviceIds = request.Services.Select(s => s.ServiceId).ToList();
        if (serviceIds.Distinct().Count() != serviceIds.Count)
            throw new AppException(400, "DUPLICATE_SERVICE", "Danh sách dịch vụ bị lặp.");

        var serviceMap = await _db.Services
            .Where(s => serviceIds.Contains(s.Id) && s.IsActive)
            .ToDictionaryAsync(s => s.Id);
        if (serviceMap.Count != serviceIds.Count)
            throw new AppException(400, "SERVICE_NOT_FOUND", "Có dịch vụ không tồn tại hoặc đã ngừng sử dụng.");

        // Giá được SAO CHÉP vào từng dòng: đổi giá sau này không ảnh hưởng hóa đơn đã lập
        var details = new List<InvoiceDetail>
        {
            new()
            {
                ItemType = InvoiceItemType.Examination,
                Description = "Phí khám bệnh",
                Quantity = 1,
                UnitPrice = appt.Doctor.ConsultationFee
            }
        };

        foreach (var item in request.Services)
        {
            var service = serviceMap[item.ServiceId];
            details.Add(new InvoiceDetail
            {
                ItemType = InvoiceItemType.Service,
                ServiceId = service.Id,
                Description = service.Name,
                Quantity = item.Quantity,
                UnitPrice = service.Price
            });
        }

        var prescription = appt.MedicalRecord?.Prescription;
        if (prescription is not null)
        {
            foreach (var d in prescription.Details)
            {
                details.Add(new InvoiceDetail
                {
                    ItemType = InvoiceItemType.Medicine,
                    MedicineId = d.MedicineId,
                    Description = $"{d.Medicine.Name} ({d.Medicine.Unit})",
                    Quantity = d.Quantity,
                    UnitPrice = d.Medicine.Price
                });
            }
        }

        var invoice = new Invoice
        {
            AppointmentId = appt.Id,
            CreatedByUserId = receptionistUserId,
            Status = InvoiceStatus.Unpaid,
            CreatedAt = _time.GetUtcNow().UtcDateTime,
            TotalAmount = details.Sum(d => d.Quantity * d.UnitPrice),
            Details = details
        };

        _db.Invoices.Add(invoice);
        try
        {
            await _db.SaveChangesAsync();
        }
        catch (DbUpdateException ex) when (ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            throw new AppException(409, "INVOICE_EXISTS", "Lượt khám này đã có hóa đơn.");
        }

        return await GetDtoAsync(invoice.Id);
    }

    public async Task<InvoiceDto> PayAsync(int id, string paymentMethod)
    {
        var method = PaymentMethods.FirstOrDefault(m =>
                string.Equals(m, paymentMethod?.Trim(), StringComparison.OrdinalIgnoreCase))
            ?? throw new AppException(400, "INVALID_PAYMENT_METHOD",
                $"Phương thức thanh toán phải là: {string.Join(", ", PaymentMethods)}.");

        var invoice = await _db.Invoices.FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy hóa đơn.");

        if (invoice.Status == InvoiceStatus.Paid)
            throw new AppException(409, "ALREADY_PAID", "Hóa đơn đã được thanh toán.");

        invoice.Status = InvoiceStatus.Paid;
        invoice.PaymentMethod = method;
        invoice.PaidAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync();

        return await GetDtoAsync(id);
    }

    public async Task<InvoiceDto> GetByIdAsync(int id, int userId, string role)
    {
        var invoice = await Query().FirstOrDefaultAsync(i => i.Id == id)
            ?? throw new AppException(404, "NOT_FOUND", "Không tìm thấy hóa đơn.");

        var allowed = role switch
        {
            RoleNames.Receptionist or RoleNames.Admin => true,
            RoleNames.Patient => invoice.Appointment.Patient.UserId == userId,
            _ => false
        };
        if (!allowed) throw new AppException(403, "FORBIDDEN", "Bạn không có quyền xem hóa đơn này.");

        return ToDto(invoice);
    }

    public async Task<List<InvoiceDto>> GetListAsync(DateOnly? date, InvoiceStatus? status)
    {
        var query = Query();

        if (date.HasValue)
        {
            // Ngày theo giờ Việt Nam, còn CreatedAt lưu UTC => lùi 7 giờ để ra mốc UTC
            var fromUtc = date.Value.ToDateTime(TimeOnly.MinValue).AddHours(-7);
            var toUtc = fromUtc.AddDays(1);
            query = query.Where(i => i.CreatedAt >= fromUtc && i.CreatedAt < toUtc);
        }

        if (status.HasValue)
            query = query.Where(i => i.Status == status.Value);

        var list = await query.OrderByDescending(i => i.CreatedAt).ToListAsync();
        return list.Select(ToDto).ToList();
    }

    public async Task<List<InvoiceDto>> GetMyAsync(int userId)
    {
        var list = await Query()
            .Where(i => i.Appointment.Patient.UserId == userId)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync();
        return list.Select(ToDto).ToList();
    }

    // ---------- Hàm hỗ trợ ----------
    private IQueryable<Invoice> Query() => _db.Invoices.AsNoTracking()
        .Include(i => i.Details)
        .Include(i => i.CreatedBy)
        .Include(i => i.Appointment).ThenInclude(a => a.Patient).ThenInclude(p => p.User)
        .Include(i => i.Appointment).ThenInclude(a => a.Doctor).ThenInclude(d => d.User);

    private async Task<InvoiceDto> GetDtoAsync(int id) =>
        ToDto(await Query().FirstAsync(i => i.Id == id));

    private static InvoiceDto ToDto(Invoice i) => new(
        i.Id, i.AppointmentId, i.Appointment.AppointmentDate,
        i.Appointment.PatientId, i.Appointment.Patient.User.FullName,
        i.Appointment.Doctor.User.FullName,
        i.TotalAmount, i.Status, i.Status.ToString(),
        i.PaymentMethod, i.PaidAt, i.CreatedAt, i.CreatedBy.FullName,
        i.Details.OrderBy(d => d.Id)
            .Select(d => new InvoiceItemDto(
                d.Id, d.ItemType, d.ItemType.ToString(),
                d.Description, d.Quantity, d.UnitPrice, d.Quantity * d.UnitPrice))
            .ToList());
}