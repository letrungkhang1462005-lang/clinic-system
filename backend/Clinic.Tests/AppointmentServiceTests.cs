using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Enums;
using Clinic.Core.Exceptions;
using Xunit;

namespace Clinic.Tests;

public class AppointmentServiceTests
{
    private static CreateAppointmentRequest Req(
        TestFixture f, DateOnly date, int hour, int minute, int? patientId = null) => new()
        {
            DoctorId = f.DoctorId,
            Date = date,
            StartTime = new TimeOnly(hour, minute),
            PatientId = patientId
        };

    private static async Task AssertError(Func<Task> action, int status, string code)
    {
        var ex = await Assert.ThrowsAsync<AppException>(action);
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(code, ex.Code);
    }

    // ---------- Đặt lịch ----------

    [Fact]
    public async Task Patient_books_a_free_slot_and_it_starts_as_Pending()
    {
        using var f = new TestFixture();

        var result = await f.Appointments.CreateAsync(
            f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Monday, 8, 0));

        Assert.Equal(AppointmentStatus.Pending, result.Status);
        Assert.Equal(f.PatientAId, result.PatientId);
        Assert.Equal(new TimeOnly(8, 30), result.EndTime);
    }

    [Fact]
    public async Task Receptionist_walk_in_booking_is_Confirmed_immediately()
    {
        using var f = new TestFixture();

        var result = await f.Appointments.CreateAsync(
            f.ReceptionistUserId, RoleNames.Receptionist, Req(f, TestDates.Monday, 8, 0, f.PatientAId));

        Assert.Equal(AppointmentStatus.Confirmed, result.Status);
    }

    [Fact]
    public async Task Receptionist_must_name_a_patient()
    {
        using var f = new TestFixture();

        await AssertError(
            () => f.Appointments.CreateAsync(
                f.ReceptionistUserId, RoleNames.Receptionist, Req(f, TestDates.Monday, 8, 0)),
            400, "PATIENT_NOT_FOUND");
    }

    [Fact]
    public async Task Booking_a_slot_that_is_already_taken_fails_with_409()
    {
        using var f = new TestFixture();
        await f.Appointments.CreateAsync(f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Monday, 8, 0));

        await AssertError(
            () => f.Appointments.CreateAsync(f.PatientBUserId, RoleNames.Patient, Req(f, TestDates.Monday, 8, 0)),
            409, "SLOT_TAKEN");
    }

    [Fact]
    public async Task Booking_a_time_in_the_past_fails()
    {
        using var f = new TestFixture();

        // Bây giờ là 08:00 ngày 12/11, slot 07:30 đã qua
        await AssertError(
            () => f.Appointments.CreateAsync(f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Today, 7, 30)),
            400, "PAST_TIME");
    }

    [Fact]
    public async Task Booking_outside_the_doctors_schedule_fails()
    {
        using var f = new TestFixture();

        await AssertError(
            () => f.Appointments.CreateAsync(f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Monday, 12, 0)),
            400, "SLOT_NOT_FOUND");

        await AssertError(
            () => f.Appointments.CreateAsync(f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Sunday, 8, 0)),
            400, "SLOT_NOT_FOUND");
    }

    [Fact]
    public async Task Booking_more_than_90_days_ahead_fails()
    {
        using var f = new TestFixture();

        await AssertError(
            () => f.Appointments.CreateAsync(
                f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Monday.AddDays(120), 8, 0)),
            400, "TOO_FAR");
    }

    // ---------- Hủy lịch ----------

    [Fact]
    public async Task Cancelled_slot_can_be_booked_by_someone_else()
    {
        using var f = new TestFixture();
        var first = await f.Appointments.CreateAsync(
            f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Monday, 8, 0));

        var cancelled = await f.Appointments.CancelAsync(first.Id, f.PatientAUserId, RoleNames.Patient, "Bận");
        Assert.Equal(AppointmentStatus.Cancelled, cancelled.Status);

        var second = await f.Appointments.CreateAsync(
            f.PatientBUserId, RoleNames.Patient, Req(f, TestDates.Monday, 8, 0));
        Assert.Equal(f.PatientBId, second.PatientId);
    }

    [Fact]
    public async Task Patient_cannot_cancel_within_2_hours_of_the_appointment()
    {
        using var f = new TestFixture();

        // Bây giờ 08:00, lịch 09:30 chỉ còn 1,5 giờ
        var appt = await f.Appointments.CreateAsync(
            f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Today, 9, 30));

        await AssertError(
            () => f.Appointments.CancelAsync(appt.Id, f.PatientAUserId, RoleNames.Patient, null),
            400, "TOO_LATE_TO_CANCEL");
    }

    [Fact]
    public async Task Receptionist_can_cancel_even_within_2_hours()
    {
        using var f = new TestFixture();
        var appt = await f.Appointments.CreateAsync(
            f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Today, 9, 30));

        var result = await f.Appointments.CancelAsync(appt.Id, f.ReceptionistUserId, RoleNames.Receptionist, "BN gọi báo hủy");

        Assert.Equal(AppointmentStatus.Cancelled, result.Status);
    }

    [Fact]
    public async Task Patient_cannot_cancel_someone_elses_appointment()
    {
        using var f = new TestFixture();
        var appt = await f.Appointments.CreateAsync(
            f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Monday, 8, 0));

        await AssertError(
            () => f.Appointments.CancelAsync(appt.Id, f.PatientBUserId, RoleNames.Patient, null),
            403, "FORBIDDEN");
    }

    // ---------- Xác nhận và tiếp đón ----------

    [Fact]
    public async Task Confirming_twice_fails_with_INVALID_STATUS()
    {
        using var f = new TestFixture();
        var appt = await f.Appointments.CreateAsync(
            f.PatientAUserId, RoleNames.Patient, Req(f, TestDates.Monday, 8, 0));

        var confirmed = await f.Appointments.ConfirmAsync(appt.Id);
        Assert.Equal(AppointmentStatus.Confirmed, confirmed.Status);

        await AssertError(() => f.Appointments.ConfirmAsync(appt.Id), 409, "INVALID_STATUS");
    }

    [Fact]
    public async Task Check_in_assigns_queue_numbers_in_order()
    {
        using var f = new TestFixture();
        var a1 = await f.Appointments.CreateAsync(
            f.ReceptionistUserId, RoleNames.Receptionist, Req(f, TestDates.Today, 9, 30, f.PatientAId));
        var a2 = await f.Appointments.CreateAsync(
            f.ReceptionistUserId, RoleNames.Receptionist, Req(f, TestDates.Today, 10, 0, f.PatientBId));

        var first = await f.Appointments.CheckInAsync(a1.Id);
        var second = await f.Appointments.CheckInAsync(a2.Id);

        Assert.Equal(1, first.QueueNumber);
        Assert.Equal(2, second.QueueNumber);
        await AssertError(() => f.Appointments.CheckInAsync(a1.Id), 409, "ALREADY_CHECKED_IN");
    }

    [Fact]
    public async Task Check_in_is_only_allowed_on_the_day_of_the_appointment()
    {
        using var f = new TestFixture();
        var appt = await f.Appointments.CreateAsync(
            f.ReceptionistUserId, RoleNames.Receptionist, Req(f, TestDates.Monday, 8, 0, f.PatientAId));

        await AssertError(() => f.Appointments.CheckInAsync(appt.Id), 400, "NOT_TODAY");
    }
}