using Clinic.Core.Constants;
using Clinic.Core.DTOs;
using Clinic.Core.Enums;
using Clinic.Core.Exceptions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Clinic.Tests;

public class ExaminationServiceTests
{
    private static async Task AssertError(Func<Task> action, int status, string code)
    {
        var ex = await Assert.ThrowsAsync<AppException>(action);
        Assert.Equal(status, ex.StatusCode);
        Assert.Equal(code, ex.Code);
    }

    // Lễ tân đặt hộ vào hôm nay: lịch có trạng thái Confirmed ngay
    private static Task<AppointmentDto> BookTodayAsync(TestFixture f, int hour, int minute, int patientId) =>
        f.Appointments.CreateAsync(f.ReceptionistUserId, RoleNames.Receptionist, new CreateAppointmentRequest
        {
            DoctorId = f.DoctorId,
            Date = TestDates.Today,
            StartTime = new TimeOnly(hour, minute),
            PatientId = patientId
        });

    private static CreateMedicalRecordRequest RecordReq(int appointmentId) => new()
    {
        AppointmentId = appointmentId,
        Symptoms = "Đau đầu",
        Diagnosis = "Cảm cúm"
    };

    private static CreatePrescriptionRequest RxReq(int recordId, params int[] medicineIds) => new()
    {
        MedicalRecordId = recordId,
        Details = medicineIds.Select(id => new PrescriptionItemRequest
        {
            MedicineId = id,
            Quantity = 10,
            Dosage = "Sáng 1 viên, tối 1 viên",
            DurationDays = 5
        }).ToList()
    };

    private static async Task<MedicalRecordDto> WriteRecordAsync(TestFixture f)
    {
        var appt = await BookTodayAsync(f, 9, 30, f.PatientAId);
        return await f.Examinations.CreateRecordAsync(f.DoctorUserId, RecordReq(appt.Id));
    }

    // ---------- Bệnh án ----------

    [Fact]
    public async Task Writing_a_record_completes_the_appointment()
    {
        using var f = new TestFixture();
        var appt = await BookTodayAsync(f, 9, 30, f.PatientAId);

        var record = await f.Examinations.CreateRecordAsync(f.DoctorUserId, RecordReq(appt.Id));

        Assert.Equal("Cảm cúm", record.Diagnosis);
        var status = await f.Db.Appointments.AsNoTracking()
            .Where(a => a.Id == appt.Id).Select(a => a.Status).SingleAsync();
        Assert.Equal(AppointmentStatus.Completed, status);
    }

    [Fact]
    public async Task Another_doctor_cannot_write_the_record()
    {
        using var f = new TestFixture();
        var appt = await BookTodayAsync(f, 9, 30, f.PatientAId);

        await AssertError(
            () => f.Examinations.CreateRecordAsync(f.Doctor2UserId, RecordReq(appt.Id)),
            403, "FORBIDDEN");
    }

    [Fact]
    public async Task Pending_appointment_cannot_receive_a_record()
    {
        using var f = new TestFixture();
        // Bệnh nhân tự đặt => Pending, chưa được lễ tân xác nhận
        var appt = await f.Appointments.CreateAsync(f.PatientAUserId, RoleNames.Patient,
            new CreateAppointmentRequest { DoctorId = f.DoctorId, Date = TestDates.Today, StartTime = new TimeOnly(9, 30) });

        await AssertError(
            () => f.Examinations.CreateRecordAsync(f.DoctorUserId, RecordReq(appt.Id)),
            409, "INVALID_STATUS");
    }

    [Fact]
    public async Task Future_appointment_cannot_receive_a_record()
    {
        using var f = new TestFixture();
        var appt = await f.Appointments.CreateAsync(f.ReceptionistUserId, RoleNames.Receptionist,
            new CreateAppointmentRequest
            {
                DoctorId = f.DoctorId,
                Date = TestDates.Monday,
                StartTime = new TimeOnly(8, 0),
                PatientId = f.PatientAId
            });

        await AssertError(
            () => f.Examinations.CreateRecordAsync(f.DoctorUserId, RecordReq(appt.Id)),
            400, "NOT_YET");
    }

    [Fact]
    public async Task Second_record_for_the_same_appointment_is_rejected()
    {
        using var f = new TestFixture();
        var appt = await BookTodayAsync(f, 9, 30, f.PatientAId);
        await f.Examinations.CreateRecordAsync(f.DoctorUserId, RecordReq(appt.Id));

        await AssertError(
            () => f.Examinations.CreateRecordAsync(f.DoctorUserId, RecordReq(appt.Id)),
            409, "INVALID_STATUS");
    }

    [Fact]
    public async Task Record_can_be_edited_the_same_day_but_is_locked_the_next_day()
    {
        using var f = new TestFixture();
        var record = await WriteRecordAsync(f);
        var edit = new UpdateMedicalRecordRequest { Diagnosis = "Cảm cúm, viêm họng nhẹ" };

        var updated = await f.Examinations.UpdateRecordAsync(record.Id, f.DoctorUserId, edit);
        Assert.Equal("Cảm cúm, viêm họng nhẹ", updated.Diagnosis);

        f.Clock.Advance(TimeSpan.FromDays(1)); // tua sang ngày hôm sau

        await AssertError(
            () => f.Examinations.UpdateRecordAsync(record.Id, f.DoctorUserId, edit),
            409, "RECORD_LOCKED");
    }

    // ---------- Đơn thuốc ----------

    [Fact]
    public async Task Prescription_is_created_with_all_items()
    {
        using var f = new TestFixture();
        var record = await WriteRecordAsync(f);

        var rx = await f.Examinations.CreatePrescriptionAsync(
            f.DoctorUserId, RxReq(record.Id, f.Medicine1Id, f.Medicine2Id));

        Assert.Equal(2, rx.Items.Count);
        Assert.Contains(rx.Items, i => i.MedicineName == "Paracetamol 500mg");
    }

    [Fact]
    public async Task Prescription_rejects_a_repeated_medicine()
    {
        using var f = new TestFixture();
        var record = await WriteRecordAsync(f);

        await AssertError(
            () => f.Examinations.CreatePrescriptionAsync(
                f.DoctorUserId, RxReq(record.Id, f.Medicine1Id, f.Medicine1Id)),
            400, "DUPLICATE_MEDICINE");
    }

    [Fact]
    public async Task Prescription_rejects_an_inactive_medicine()
    {
        using var f = new TestFixture();
        var record = await WriteRecordAsync(f);

        await AssertError(
            () => f.Examinations.CreatePrescriptionAsync(
                f.DoctorUserId, RxReq(record.Id, f.Medicine1Id, f.InactiveMedicineId)),
            400, "MEDICINE_NOT_FOUND");
    }

    [Fact]
    public async Task Only_one_prescription_per_record()
    {
        using var f = new TestFixture();
        var record = await WriteRecordAsync(f);
        await f.Examinations.CreatePrescriptionAsync(f.DoctorUserId, RxReq(record.Id, f.Medicine1Id));

        await AssertError(
            () => f.Examinations.CreatePrescriptionAsync(f.DoctorUserId, RxReq(record.Id, f.Medicine2Id)),
            409, "PRESCRIPTION_EXISTS");
    }

    // ---------- Quyền xem và lịch sử ----------

    [Fact]
    public async Task Patient_reads_own_record_but_not_someone_elses()
    {
        using var f = new TestFixture();
        var record = await WriteRecordAsync(f);

        var own = await f.Examinations.GetRecordAsync(record.Id, f.PatientAUserId, RoleNames.Patient);
        Assert.Equal("Cảm cúm", own.Diagnosis);

        await AssertError(
            () => f.Examinations.GetRecordAsync(record.Id, f.PatientBUserId, RoleNames.Patient),
            403, "FORBIDDEN");
    }

    [Fact]
    public async Task Doctor_can_only_see_history_of_patients_who_visited_them()
    {
        using var f = new TestFixture();
        await WriteRecordAsync(f);

        var history = await f.Examinations.GetHistoryAsync(f.PatientAId, f.DoctorUserId, RoleNames.Doctor);
        Assert.Single(history);

        await AssertError(
            () => f.Examinations.GetHistoryAsync(f.PatientAId, f.Doctor2UserId, RoleNames.Doctor),
            403, "FORBIDDEN");
    }

    [Fact]
    public async Task Patient_cannot_read_another_patients_history()
    {
        using var f = new TestFixture();
        await WriteRecordAsync(f);

        await AssertError(
            () => f.Examinations.GetHistoryAsync(f.PatientAId, f.PatientBUserId, RoleNames.Patient),
            403, "FORBIDDEN");

        var mine = await f.Examinations.GetMyHistoryAsync(f.PatientAUserId);
        Assert.Single(mine);
    }
}