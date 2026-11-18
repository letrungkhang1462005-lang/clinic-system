using Clinic.Core.Constants;
using Clinic.Core.Entities;
using Microsoft.EntityFrameworkCore;
using Clinic.Core.Common;
using Clinic.Core.Enums;

namespace Clinic.Infrastructure.Data;

public static class DbSeeder
{
    public static async Task SeedAsync(ClinicDbContext db)
    {
        await EnsureUserAsync(db, "admin@clinic.local", "Quản trị viên", "Admin@123", RoleNames.Admin);
        await EnsureUserAsync(db, "letan@clinic.local", "Lễ tân Demo", "Letan@123", RoleNames.Receptionist);

        await EnsureDoctorAsync(db, "bs.an@clinic.local", "BS. Nguyễn Văn An", 1, "Thạc sĩ, Bác sĩ", 10, 200000);
        await EnsureDoctorAsync(db, "bs.binh@clinic.local", "BS. Trần Thị Bình", 2, "Bác sĩ chuyên khoa I", 8, 250000);
        await EnsureDoctorAsync(db, "bs.cuong@clinic.local", "BS. Lê Minh Cường", 3, "Tiến sĩ, Bác sĩ", 15, 300000);

        // Thêm dữ liệu mẫu thuốc và dịch vụ
        await EnsureCatalogAsync(db);
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

    private static async Task EnsureDoctorAsync(
        ClinicDbContext db, string email, string fullName,
        int specialtyId, string degree, int years, decimal fee)
    {
        if (await db.Users.AnyAsync(u => u.Email == email)) return;

        var role = await db.Roles.FirstAsync(r => r.Name == RoleNames.Doctor);
        var doctor = new Doctor
        {
            User = new User
            {
                Email = email,
                FullName = fullName,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword("Doctor@123"),
                Role = role
            },
            SpecialtyId = specialtyId,
            Degree = degree,
            ExperienceYears = years,
            ConsultationFee = fee
        };

        var workDays = new[]
        {
            DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday,
            DayOfWeek.Thursday, DayOfWeek.Friday
        };

        foreach (var day in workDays)
        {
            doctor.Schedules.Add(new DoctorSchedule
            {
                DayOfWeek = day,
                StartTime = new TimeOnly(7, 30),
                EndTime = new TimeOnly(11, 30),
                SlotMinutes = 30
            });
            doctor.Schedules.Add(new DoctorSchedule
            {
                DayOfWeek = day,
                StartTime = new TimeOnly(13, 30),
                EndTime = new TimeOnly(16, 30),
                SlotMinutes = 30
            });
        }

        db.Doctors.Add(doctor);
        await db.SaveChangesAsync();
    }

    private static async Task EnsureCatalogAsync(ClinicDbContext db)
    {
        // Dữ liệu mẫu để demo, giá chỉ mang tính minh họa
        if (!await db.Medicines.AnyAsync())
        {
            db.Medicines.AddRange(
                new Medicine { Name = "Paracetamol 500mg", Unit = "viên", Price = 1500 },
                new Medicine { Name = "Amoxicillin 500mg", Unit = "viên", Price = 3000 },
                new Medicine { Name = "Cetirizine 10mg", Unit = "viên", Price = 2000 },
                new Medicine { Name = "Omeprazole 20mg", Unit = "viên", Price = 2500 },
                new Medicine { Name = "Loratadin 10mg", Unit = "viên", Price = 1800 },
                new Medicine { Name = "Ibuprofen 400mg", Unit = "viên", Price = 2200 },
                new Medicine { Name = "Vitamin C 500mg", Unit = "viên", Price = 1000 },
                new Medicine { Name = "Natri clorid 0,9%", Unit = "chai", Price = 8000 }
            );
        }

        if (!await db.Services.AnyAsync())
        {
            db.Services.AddRange(
                new Service { Name = "Xét nghiệm máu tổng quát", Price = 150000 },
                new Service { Name = "Siêu âm ổ bụng", Price = 200000 },
                new Service { Name = "Điện tâm đồ (ECG)", Price = 120000 },
                new Service { Name = "X-quang ngực thẳng", Price = 180000 }
            );
        }

        await db.SaveChangesAsync();
    }
    private static DateTime ToUtc(DateOnly date, TimeOnly time) => date.ToDateTime(time).AddHours(-7);

    private static double SlotWeight(int hour) => hour switch
    {
        8 or 9 => 1.0,
        10 => 0.9,
        14 => 0.7,
        7 or 11 => 0.6,
        15 => 0.6,
        13 => 0.5,
        _ => 0.4
    };

    // Dữ liệu lịch sử để dashboard có số liệu. Chạy một lần, chạy lại không tạo trùng.
    public static async Task SeedDemoHistoryAsync(ClinicDbContext db)
    {
        const string marker = "walkin-demo";
        if (await db.Users.AnyAsync(u => u.Email.StartsWith(marker))) return;

        var doctors = await db.Doctors.Include(d => d.Schedules).Where(d => d.User.IsActive).ToListAsync();
        var medicines = await db.Medicines.Where(m => m.IsActive).ToListAsync();
        var services = await db.Services.Where(s => s.IsActive).ToListAsync();
        var receptionist = await db.Users.FirstOrDefaultAsync(u => u.Role.Name == RoleNames.Receptionist);
        var patientRole = await db.Roles.FirstAsync(r => r.Name == RoleNames.Patient);
        if (doctors.Count == 0 || medicines.Count == 0 || receptionist is null) return;

        var rnd = new Random(2026); // cố định để lần nào cũng ra cùng một bộ dữ liệu
        var nowVn = DateTime.UtcNow.AddHours(7);
        var todayVn = DateOnly.FromDateTime(nowVn);
        var hash = BCrypt.Net.BCrypt.HashPassword(Guid.NewGuid().ToString());

        // 1) Bệnh nhân demo (không đăng nhập được, giống bệnh nhân khám trực tiếp)
        var names = new[]
        {
            "Nguyễn Thị Hoa", "Trần Văn Nam", "Lê Thị Mai", "Phạm Quốc Huy", "Hoàng Thị Lan",
            "Vũ Minh Tuấn", "Đặng Thu Hà", "Bùi Văn Long", "Đỗ Thị Ngọc", "Ngô Quang Vinh",
            "Dương Thị Thảo", "Lý Văn Hùng", "Phan Thị Yến", "Võ Minh Khôi", "Trịnh Thị Liên"
        };

        var patients = names.Select((name, i) => new Patient
        {
            User = new User
            {
                Email = $"{marker}{i + 1}@walkin.clinic.local",
                FullName = name,
                Phone = $"09110000{i + 1:00}",
                PasswordHash = hash,
                Role = patientRole,
                IsActive = false,
                CreatedAt = ToUtc(todayVn.AddDays(-rnd.Next(1, 31)), new TimeOnly(8, 0))
            }
        }).ToList();

        db.Patients.AddRange(patients);
        await db.SaveChangesAsync();

        // 2) Lịch sử khám: 30 ngày gần nhất, bỏ thứ Bảy và Chủ nhật
        var cases = new[]
        {
            ("Sốt, ho, đau họng", "Viêm họng cấp", "Uống nhiều nước ấm, nghỉ ngơi"),
            ("Đau đầu, chóng mặt", "Căng thẳng, thiếu ngủ", "Ngủ đủ giấc, tái khám nếu kéo dài"),
            ("Đau bụng, đầy hơi", "Viêm dạ dày", "Ăn nhạt, tránh đồ cay"),
            ("Nghẹt mũi, hắt hơi", "Viêm mũi dị ứng", "Tránh bụi, giữ ấm"),
            ("Ho kéo dài về đêm", "Viêm phế quản", "Hạn chế khói bụi"),
            ("Sốt cao, mệt mỏi", "Cảm cúm", "Nghỉ ngơi, bù nước"),
            ("Nổi mẩn ngứa", "Viêm da dị ứng", "Tránh tác nhân gây dị ứng")
        };

        for (var offset = 30; offset >= 1; offset--)
        {
            var day = todayVn.AddDays(-offset);
            if (day.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) continue;

            foreach (var doctor in doctors)
            {
                var slots = SlotGenerator.Generate(doctor.Schedules, new HashSet<TimeOnly>(), day, nowVn);
                if (slots.Count == 0) continue;

                // Chọn các khung giờ khác nhau, ưu tiên buổi sáng để biểu đồ giờ cao điểm có hình dạng
                var chosen = slots
                    .OrderByDescending(s => rnd.NextDouble() * SlotWeight(s.StartTime.Hour))
                    .Take(Math.Min(slots.Count, rnd.Next(4, 10)))
                    .OrderBy(s => s.StartTime)
                    .ToList();

                var queue = 0;
                foreach (var slot in chosen)
                {
                    var patient = patients[rnd.Next(patients.Count)];
                    var visit = cases[rnd.Next(cases.Length)];
                    var cancelled = rnd.NextDouble() < 0.12;
                    var visitUtc = ToUtc(day, slot.StartTime);

                    var appt = new Appointment
                    {
                        PatientId = patient.Id,
                        DoctorId = doctor.Id,
                        AppointmentDate = day,
                        StartTime = slot.StartTime,
                        EndTime = slot.EndTime,
                        Reason = visit.Item1,
                        CreatedAt = visitUtc.AddDays(-rnd.Next(1, 4)),
                        Status = cancelled ? AppointmentStatus.Cancelled : AppointmentStatus.Completed,
                        CancelReason = cancelled ? "Bệnh nhân bận việc đột xuất" : null
                    };

                    if (!cancelled)
                    {
                        appt.QueueNumber = ++queue;
                        var recordTime = visitUtc.AddMinutes(20);

                        var record = new MedicalRecord
                        {
                            Symptoms = visit.Item1,
                            Diagnosis = visit.Item2,
                            Notes = visit.Item3,
                            CreatedAt = recordTime
                        };

                        var lines = new List<InvoiceDetail>
                        {
                            new()
                            {
                                ItemType = InvoiceItemType.Examination,
                                Description = "Phí khám bệnh",
                                Quantity = 1,
                                UnitPrice = doctor.ConsultationFee
                            }
                        };

                        if (services.Count > 0 && rnd.NextDouble() < 0.35)
                        {
                            var service = services[rnd.Next(services.Count)];
                            lines.Add(new InvoiceDetail
                            {
                                ItemType = InvoiceItemType.Service,
                                ServiceId = service.Id,
                                Description = service.Name,
                                Quantity = 1,
                                UnitPrice = service.Price
                            });
                        }

                        if (rnd.NextDouble() < 0.7)
                        {
                            var prescription = new Prescription { CreatedAt = recordTime };
                            foreach (var med in medicines.OrderBy(_ => rnd.Next()).Take(rnd.Next(1, 4)))
                            {
                                var qty = rnd.Next(5, 21);
                                prescription.Details.Add(new PrescriptionDetail
                                {
                                    MedicineId = med.Id,
                                    Quantity = qty,
                                    Dosage = "Sáng 1 viên, tối 1 viên",
                                    DurationDays = 5
                                });
                                lines.Add(new InvoiceDetail
                                {
                                    ItemType = InvoiceItemType.Medicine,
                                    MedicineId = med.Id,
                                    Description = $"{med.Name} ({med.Unit})",
                                    Quantity = qty,
                                    UnitPrice = med.Price
                                });
                            }
                            record.Prescription = prescription;
                        }

                        // Hóa đơn của vài ngày gần nhất để một phần còn nợ, cho thẻ "công nợ" có số liệu
                        var paid = offset > 2 || rnd.NextDouble() < 0.7;

                        appt.MedicalRecord = record;
                        appt.Invoice = new Invoice
                        {
                            CreatedByUserId = receptionist.Id,
                            Status = paid ? InvoiceStatus.Paid : InvoiceStatus.Unpaid,
                            PaymentMethod = paid ? (rnd.NextDouble() < 0.6 ? "Cash" : "Transfer") : null,
                            CreatedAt = recordTime.AddMinutes(10),
                            PaidAt = paid ? recordTime.AddMinutes(20) : null,
                            TotalAmount = lines.Sum(l => l.Quantity * l.UnitPrice),
                            Details = lines
                        };
                    }

                    db.Appointments.Add(appt);
                }
            }
        }

        await db.SaveChangesAsync();
    }
}