using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Enums;
using Clinic.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinic.Tests;

internal static class Helpers
{
    public static async Task AssertError(Func<Task> action, int status, string code)
    {
        var ex = await Assert.ThrowsAsync<AppException>(action);
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(code, ex.Code);
    }

    // Tạo một lượt khám đã hoàn tất vào hôm nay (có bệnh án, tùy chọn có đơn thuốc)
    public static async Task<AppointmentDto> CompleteVisitAsync(
        TestFixture f, int hour, int minute, int patientId, bool withPrescription = false)
    {
        var appt = await f.Appointments.CreateAsync(f.ReceptionistUserId, RoleNames.Receptionist,
            new CreateAppointmentRequest
            {
                DoctorId = f.DoctorId,
                Date = TestDates.Today,
                StartTime = new TimeOnly(hour, minute),
                PatientId = patientId
            });

        var record = await f.Examinations.CreateRecordAsync(f.DoctorUserId,
            new CreateMedicalRecordRequest { AppointmentId = appt.Id, Diagnosis = "Cảm cúm" });

        if (withPrescription)
        {
            await f.Examinations.CreatePrescriptionAsync(f.DoctorUserId, new CreatePrescriptionRequest
            {
                MedicalRecordId = record.Id,
                Details = new List<PrescriptionItemRequest>
                {
                    new() { MedicineId = f.Medicine1Id, Quantity = 10, Dosage = "Sáng 1 viên", DurationDays = 5 },
                    new() { MedicineId = f.Medicine2Id, Quantity = 5, Dosage = "Tối 1 viên", DurationDays = 5 }
                }
            });
        }

        return appt;
    }
}

public class PatientServiceTests
{
    [Fact]
    public async Task Walk_in_patient_is_created_without_a_login()
    {
        using var f = new TestFixture();

        var dto = await f.Patients.CreateWalkInAsync(
            new CreatePatientRequest { FullName = "Trần Thị Walk", Phone = "0912345678" });

        Assert.False(dto.HasAccount);
        Assert.Null(dto.Email);
        var active = await f.Db.Patients.Where(p => p.Id == dto.Id).Select(p => p.User.IsActive).SingleAsync();
        Assert.False(active);
    }

    [Fact]
    public async Task Same_phone_number_is_rejected()
    {
        using var f = new TestFixture();
        var req = new CreatePatientRequest { FullName = "Trần Thị Walk", Phone = "0912345678" };
        await f.Patients.CreateWalkInAsync(req);

        await Helpers.AssertError(() => f.Patients.CreateWalkInAsync(req), 409, "PATIENT_EXISTS");
    }

    [Fact]
    public async Task Receptionist_can_book_for_a_walk_in_patient()
    {
        using var f = new TestFixture();
        var walkIn = await f.Patients.CreateWalkInAsync(
            new CreatePatientRequest { FullName = "Trần Thị Walk", Phone = "0912345678" });

        var appt = await f.Appointments.CreateAsync(f.ReceptionistUserId, RoleNames.Receptionist,
            new CreateAppointmentRequest
            {
                DoctorId = f.DoctorId,
                Date = TestDates.Monday,
                StartTime = new TimeOnly(8, 0),
                PatientId = walkIn.Id
            });

        Assert.Equal(AppointmentStatus.Confirmed, appt.Status);
        Assert.Equal("Trần Thị Walk", appt.PatientName);
    }

    [Fact]
    public async Task Search_finds_patients_by_phone_or_name()
    {
        using var f = new TestFixture();
        await f.Patients.CreateWalkInAsync(new CreatePatientRequest { FullName = "Lê Văn Một", Phone = "0912345678" });
        await f.Patients.CreateWalkInAsync(new CreatePatientRequest { FullName = "Phạm Thị Hai", Phone = "0987654321" });

        var byPhone = await f.Patients.GetListAsync(f.ReceptionistUserId, RoleNames.Receptionist, "0912");
        var byName = await f.Patients.GetListAsync(f.ReceptionistUserId, RoleNames.Receptionist, "Hai");

        Assert.Single(byPhone);
        Assert.Equal("Lê Văn Một", byPhone[0].FullName);
        Assert.Single(byName);
    }

    [Fact]
    public async Task Doctor_only_sees_patients_who_visited_them()
    {
        using var f = new TestFixture();
        await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);

        var mine = await f.Patients.GetListAsync(f.DoctorUserId, RoleNames.Doctor, null);
        Assert.Single(mine);
        Assert.Equal(f.PatientAId, mine[0].Id);

        var other = await f.Patients.GetListAsync(f.Doctor2UserId, RoleNames.Doctor, null);
        Assert.Empty(other);

        await Helpers.AssertError(
            () => f.Patients.GetByIdAsync(f.PatientAId, f.Doctor2UserId, RoleNames.Doctor), 403, "FORBIDDEN");
    }
}

public class InvoiceServiceTests
{
    private static CreateInvoiceRequest Req(int appointmentId, params (int ServiceId, int Quantity)[] services) => new()
    {
        AppointmentId = appointmentId,
        Services = services.Select(s => new InvoiceServiceItem { ServiceId = s.ServiceId, Quantity = s.Quantity }).ToList()
    };

    [Fact]
    public async Task Invoice_collects_exam_fee_services_and_medicines()
    {
        using var f = new TestFixture();
        var appt = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId, withPrescription: true);

        var inv = await f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id, (f.Service1Id, 1)));

        // 200.000 (khám) + 150.000 (xét nghiệm) + 10x1.500 + 5x2.000 (thuốc)
        Assert.Equal(375000m, inv.TotalAmount);
        Assert.Equal(4, inv.Items.Count);
        Assert.Equal(InvoiceStatus.Unpaid, inv.Status);
        Assert.Contains(inv.Items, i => i.ItemType == InvoiceItemType.Examination && i.UnitPrice == 200000m);
        Assert.Equal(1, inv.Items.Count(i => i.ItemType == InvoiceItemType.Service));
        Assert.Equal(2, inv.Items.Count(i => i.ItemType == InvoiceItemType.Medicine));
    }

    [Fact]
    public async Task Visit_without_prescription_only_has_the_exam_fee()
    {
        using var f = new TestFixture();
        var appt = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);

        var inv = await f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id));

        Assert.Single(inv.Items);
        Assert.Equal(200000m, inv.TotalAmount);
    }

    [Fact]
    public async Task Cannot_invoice_a_visit_that_is_not_completed()
    {
        using var f = new TestFixture();
        var appt = await f.Appointments.CreateAsync(f.ReceptionistUserId, RoleNames.Receptionist,
            new CreateAppointmentRequest
            {
                DoctorId = f.DoctorId,
                Date = TestDates.Today,
                StartTime = new TimeOnly(9, 30),
                PatientId = f.PatientAId
            });

        await Helpers.AssertError(
            () => f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id)), 409, "INVALID_STATUS");
    }

    [Fact]
    public async Task A_visit_can_only_be_invoiced_once()
    {
        using var f = new TestFixture();
        var appt = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);
        await f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id));

        await Helpers.AssertError(
            () => f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id)), 409, "INVOICE_EXISTS");
    }

    [Fact]
    public async Task Inactive_service_is_rejected()
    {
        using var f = new TestFixture();
        var appt = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);

        await Helpers.AssertError(
            () => f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id, (f.InactiveServiceId, 1))),
            400, "SERVICE_NOT_FOUND");
    }

    [Fact]
    public async Task Repeated_service_is_rejected()
    {
        using var f = new TestFixture();
        var appt = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);

        await Helpers.AssertError(
            () => f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id, (f.Service1Id, 1), (f.Service1Id, 2))),
            400, "DUPLICATE_SERVICE");
    }

    [Fact]
    public async Task Invoice_keeps_its_prices_after_the_catalog_changes()
    {
        using var f = new TestFixture();
        var appt = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId, withPrescription: true);
        var inv = await f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id, (f.Service1Id, 1)));

        var medicine = await f.Db.Medicines.FindAsync(f.Medicine1Id);
        medicine!.Price = 99999m;
        await f.Db.SaveChangesAsync();

        var again = await f.Invoices.GetByIdAsync(inv.Id, f.ReceptionistUserId, RoleNames.Receptionist);
        Assert.Equal(375000m, again.TotalAmount);
    }

    [Fact]
    public async Task Paying_sets_status_method_and_time()
    {
        using var f = new TestFixture();
        var appt = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);
        var inv = await f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id));

        var paid = await f.Invoices.PayAsync(inv.Id, "cash");

        Assert.Equal(InvoiceStatus.Paid, paid.Status);
        Assert.Equal("Cash", paid.PaymentMethod);
        Assert.NotNull(paid.PaidAt);
    }

    [Fact]
    public async Task Paying_twice_is_rejected()
    {
        using var f = new TestFixture();
        var appt = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);
        var inv = await f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id));
        await f.Invoices.PayAsync(inv.Id, "Cash");

        await Helpers.AssertError(() => f.Invoices.PayAsync(inv.Id, "Transfer"), 409, "ALREADY_PAID");
    }

    [Fact]
    public async Task Unknown_payment_method_is_rejected()
    {
        using var f = new TestFixture();
        var appt = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);
        var inv = await f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id));

        await Helpers.AssertError(() => f.Invoices.PayAsync(inv.Id, "Bitcoin"), 400, "INVALID_PAYMENT_METHOD");
    }

    [Fact]
    public async Task Patient_sees_only_their_own_invoice()
    {
        using var f = new TestFixture();
        var appt = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);
        var inv = await f.Invoices.CreateAsync(f.ReceptionistUserId, Req(appt.Id));

        var own = await f.Invoices.GetByIdAsync(inv.Id, f.PatientAUserId, RoleNames.Patient);
        Assert.Equal(inv.Id, own.Id);
        Assert.Single(await f.Invoices.GetMyAsync(f.PatientAUserId));

        await Helpers.AssertError(
            () => f.Invoices.GetByIdAsync(inv.Id, f.PatientBUserId, RoleNames.Patient), 403, "FORBIDDEN");
    }

    [Fact]
    public async Task Invoice_list_can_be_filtered_by_status()
    {
        using var f = new TestFixture();
        var a1 = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);
        var a2 = await Helpers.CompleteVisitAsync(f, 10, 0, f.PatientBId);
        var inv1 = await f.Invoices.CreateAsync(f.ReceptionistUserId, Req(a1.Id));
        await f.Invoices.CreateAsync(f.ReceptionistUserId, Req(a2.Id));
        await f.Invoices.PayAsync(inv1.Id, "Cash");

        Assert.Single(await f.Invoices.GetListAsync(TestDates.Today, InvoiceStatus.Paid));
        Assert.Single(await f.Invoices.GetListAsync(TestDates.Today, InvoiceStatus.Unpaid));
        Assert.Equal(2, (await f.Invoices.GetListAsync(TestDates.Today, null)).Count);
    }
}

public class DashboardServiceTests
{
    // Kịch bản: 2 lượt khám hoàn tất (9:30, 10:00), mỗi lượt 200.000đ phí khám,
    // 1 hóa đơn đã thu, 1 chưa thu, và 1 lịch bị hủy (10:30)
    private static async Task SeedScenarioAsync(TestFixture f)
    {
        var v1 = await Helpers.CompleteVisitAsync(f, 9, 30, f.PatientAId);
        var v2 = await Helpers.CompleteVisitAsync(f, 10, 0, f.PatientBId);

        var inv1 = await f.Invoices.CreateAsync(f.ReceptionistUserId, new CreateInvoiceRequest { AppointmentId = v1.Id });
        await f.Invoices.CreateAsync(f.ReceptionistUserId, new CreateInvoiceRequest { AppointmentId = v2.Id });
        await f.Invoices.PayAsync(inv1.Id, "Cash");

        var pending = await f.Appointments.CreateAsync(f.PatientAUserId, RoleNames.Patient,
            new CreateAppointmentRequest { DoctorId = f.DoctorId, Date = TestDates.Today, StartTime = new TimeOnly(10, 30) });
        await f.Appointments.CancelAsync(pending.Id, f.ReceptionistUserId, RoleNames.Receptionist, "test");
    }

    [Fact]
    public async Task Summary_counts_revenue_and_cancel_rate()
    {
        using var f = new TestFixture();
        await SeedScenarioAsync(f);

        var s = await f.Dashboard.GetSummaryAsync(TestDates.Today, TestDates.Today);

        Assert.Equal(200000m, s.TotalRevenue);   // chỉ tính hóa đơn đã thu
        Assert.Equal(1, s.PaidInvoices);
        Assert.Equal(200000m, s.UnpaidAmount);
        Assert.Equal(3, s.TotalAppointments);
        Assert.Equal(2, s.CompletedAppointments);
        Assert.Equal(1, s.CancelledAppointments);
        Assert.Equal(33.3, s.CancelRate, 1);
    }

    [Fact]
    public async Task Revenue_by_day_fills_days_without_revenue()
    {
        using var f = new TestFixture();
        await SeedScenarioAsync(f);

        var points = await f.Dashboard.GetRevenueByDayAsync(TestDates.Today.AddDays(-2), TestDates.Today);

        Assert.Equal(3, points.Count);
        Assert.Equal(0m, points[0].Revenue);
        Assert.Equal(0m, points[1].Revenue);
        Assert.Equal(200000m, points[2].Revenue);
        Assert.Equal(1, points[2].Invoices);
    }

    [Fact]
    public async Task Top_doctors_ignores_cancelled_appointments()
    {
        using var f = new TestFixture();
        await SeedScenarioAsync(f);

        var top = await f.Dashboard.GetTopDoctorsAsync(TestDates.Today, TestDates.Today, 5);

        var only = Assert.Single(top);
        Assert.Equal("BS. Test", only.DoctorName);
        Assert.Equal(2, only.Appointments);
        Assert.Equal(2, only.Completed);
    }

    [Fact]
    public async Task Peak_hours_groups_appointments_by_hour()
    {
        using var f = new TestFixture();
        await SeedScenarioAsync(f);

        var hours = await f.Dashboard.GetPeakHoursAsync(TestDates.Today, TestDates.Today);

        Assert.Equal(new[] { 9, 10 }, hours.Select(h => h.Hour));
        Assert.All(hours, h => Assert.Equal(1, h.Appointments));
    }

    [Fact]
    public async Task Reversed_date_range_is_rejected()
    {
        using var f = new TestFixture();

        await Helpers.AssertError(
            () => f.Dashboard.GetSummaryAsync(TestDates.Today, TestDates.Today.AddDays(-5)), 400, "INVALID_RANGE");
    }
}