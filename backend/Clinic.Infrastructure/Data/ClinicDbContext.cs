using Clinic.Core.Entities;
using Clinic.Core.Enums;
using Microsoft.EntityFrameworkCore;

namespace Clinic.Infrastructure.Data;

public class ClinicDbContext : DbContext
{
    public ClinicDbContext(DbContextOptions<ClinicDbContext> options) : base(options) { }

    public DbSet<Role> Roles => Set<Role>();
    public DbSet<User> Users => Set<User>();
    public DbSet<Specialty> Specialties => Set<Specialty>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<Doctor> Doctors => Set<Doctor>();
    public DbSet<DoctorSchedule> DoctorSchedules => Set<DoctorSchedule>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<MedicalRecord> MedicalRecords => Set<MedicalRecord>();
    public DbSet<Medicine> Medicines => Set<Medicine>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionDetail> PrescriptionDetails => Set<PrescriptionDetail>();
    public DbSet<Service> Services => Set<Service>();
    public DbSet<Invoice> Invoices => Set<Invoice>();
    public DbSet<InvoiceDetail> InvoiceDetails => Set<InvoiceDetail>();

    protected override void OnModelCreating(ModelBuilder b)
    {
        base.OnModelCreating(b);

        b.Entity<Role>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(50).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.HasData(
                new Role { Id = 1, Name = "Admin" },
                new Role { Id = 2, Name = "Receptionist" },
                new Role { Id = 3, Name = "Doctor" },
                new Role { Id = 4, Name = "Patient" });
        });

        b.Entity<User>(e =>
        {
            e.Property(x => x.Email).HasMaxLength(256).IsRequired();
            e.HasIndex(x => x.Email).IsUnique();
            e.Property(x => x.PasswordHash).HasMaxLength(500).IsRequired();
            e.Property(x => x.FullName).HasMaxLength(150).IsRequired();
            e.Property(x => x.Phone).HasMaxLength(20);
            e.HasOne(x => x.Role).WithMany(r => r.Users)
                .HasForeignKey(x => x.RoleId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Specialty>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(100).IsRequired();
            e.HasIndex(x => x.Name).IsUnique();
            e.Property(x => x.Description).HasMaxLength(500);
            e.HasData(
                new Specialty { Id = 1, Name = "Nội tổng quát" },
                new Specialty { Id = 2, Name = "Nhi khoa" },
                new Specialty { Id = 3, Name = "Tai Mũi Họng" },
                new Specialty { Id = 4, Name = "Da liễu" },
                new Specialty { Id = 5, Name = "Răng Hàm Mặt" });
        });

        b.Entity<Patient>(e =>
        {
            e.Property(x => x.Gender).HasMaxLength(10);
            e.Property(x => x.Address).HasMaxLength(300);
            e.HasOne(x => x.User).WithOne()
                .HasForeignKey<Patient>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Doctor>(e =>
        {
            e.Property(x => x.Degree).HasMaxLength(100);
            e.Property(x => x.Bio).HasMaxLength(1000);
            e.Property(x => x.ConsultationFee).HasPrecision(18, 2);
            e.HasOne(x => x.User).WithOne()
                .HasForeignKey<Doctor>(x => x.UserId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Specialty).WithMany(s => s.Doctors)
                .HasForeignKey(x => x.SpecialtyId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<DoctorSchedule>(e =>
        {
            e.Property(x => x.DayOfWeek).HasConversion<byte>();
            e.HasOne(x => x.Doctor).WithMany(d => d.Schedules)
                .HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Cascade);
        });

        b.Entity<Appointment>(e =>
        {
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.Reason).HasMaxLength(500);
            e.Property(x => x.CancelReason).HasMaxLength(500);
            e.HasOne(x => x.Patient).WithMany()
                .HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Doctor).WithMany()
                .HasForeignKey(x => x.DoctorId).OnDelete(DeleteBehavior.Restrict);

            // Chốt chặn cuối cùng chống đặt trùng: một bác sĩ, một ngày, một giờ
            // chỉ có tối đa một lịch chưa bị hủy (Status 3 = Cancelled).
            e.HasIndex(x => new { x.DoctorId, x.AppointmentDate, x.StartTime })
                .IsUnique()
                .HasFilter("[Status] <> 3");
        });

        b.Entity<MedicalRecord>(e =>
        {
            e.Property(x => x.Symptoms).HasMaxLength(1000);
            e.Property(x => x.Diagnosis).HasMaxLength(1000).IsRequired();
            e.Property(x => x.Notes).HasMaxLength(1000);
            e.HasOne(x => x.Appointment).WithOne(a => a.MedicalRecord)
                .HasForeignKey<MedicalRecord>(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Medicine>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Unit).HasMaxLength(30).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.Description).HasMaxLength(500);
        });

        b.Entity<Prescription>(e =>
        {
            e.Property(x => x.Note).HasMaxLength(500);
            e.HasOne(x => x.MedicalRecord).WithOne(m => m.Prescription)
                .HasForeignKey<Prescription>(x => x.MedicalRecordId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<PrescriptionDetail>(e =>
        {
            e.Property(x => x.Dosage).HasMaxLength(200).IsRequired();
            e.HasOne(x => x.Prescription).WithMany(p => p.Details)
                .HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Medicine).WithMany()
                .HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<Service>(e =>
        {
            e.Property(x => x.Name).HasMaxLength(200).IsRequired();
            e.Property(x => x.Price).HasPrecision(18, 2);
            e.Property(x => x.Description).HasMaxLength(500);
        });

        b.Entity<Invoice>(e =>
        {
            e.Property(x => x.Status).HasConversion<byte>();
            e.Property(x => x.TotalAmount).HasPrecision(18, 2);
            e.Property(x => x.PaymentMethod).HasMaxLength(30);
            e.HasOne(x => x.Appointment).WithOne(a => a.Invoice)
                .HasForeignKey<Invoice>(x => x.AppointmentId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.CreatedBy).WithMany()
                .HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        });

        b.Entity<InvoiceDetail>(e =>
        {
            e.Property(x => x.ItemType).HasConversion<byte>();
            e.Property(x => x.Description).HasMaxLength(200).IsRequired();
            e.Property(x => x.UnitPrice).HasPrecision(18, 2);
            e.HasOne(x => x.Invoice).WithMany(i => i.Details)
                .HasForeignKey(x => x.InvoiceId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne(x => x.Service).WithMany()
                .HasForeignKey(x => x.ServiceId).OnDelete(DeleteBehavior.Restrict);
            e.HasOne(x => x.Medicine).WithMany()
                .HasForeignKey(x => x.MedicineId).OnDelete(DeleteBehavior.Restrict);
        });
    }
}