using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using PCKManagementSystem.Models;

namespace PCKManagementSystem.Data
{
    public class ApplicationDbContext : IdentityDbContext<User, IdentityRole<int>, int>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        // Убираем DbSet<Role> - его больше нет!
        public DbSet<Specialty> Specialties { get; set; }
        public DbSet<Discipline> Disciplines { get; set; }
        public DbSet<Document> Documents { get; set; }
        public DbSet<Workload> Workloads { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<AuditLog> AuditLogs { get; set; }
        public DbSet<Tasks> Tasks { get; set; }
        public DbSet<Announcement> Announcements { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // Переименовываем таблицы Identity для красоты (опционально)
            modelBuilder.Entity<User>().ToTable("Users");
            modelBuilder.Entity<IdentityRole<int>>().ToTable("Roles");
            modelBuilder.Entity<IdentityUserRole<int>>().ToTable("UserRoles");
            modelBuilder.Entity<IdentityUserClaim<int>>().ToTable("UserClaims");
            modelBuilder.Entity<IdentityUserLogin<int>>().ToTable("UserLogins");
            modelBuilder.Entity<IdentityUserToken<int>>().ToTable("UserTokens");
            modelBuilder.Entity<IdentityRoleClaim<int>>().ToTable("RoleClaims");

            // 1. Уникальный email
            modelBuilder.Entity<User>()
                .HasIndex(u => u.Email)
                .IsUnique();

            // 2. Индексы для AuditLog
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.UserId);
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.ActionDate);
            modelBuilder.Entity<AuditLog>()
                .HasIndex(a => a.EntityType);

            // 3. Индекс для Workload
            modelBuilder.Entity<Workload>()
                .HasIndex(w => new { w.TeacherId, w.AcademicYear, w.Semester });

            // 4. Связь Discipline -> Specialty
            modelBuilder.Entity<Discipline>()
                .HasOne(d => d.Specialty)
                .WithMany(s => s.Disciplines)
                .HasForeignKey(d => d.SpecialtyId)
                .OnDelete(DeleteBehavior.Restrict);

            // 5. Связь Document -> Author
            modelBuilder.Entity<Document>()
                .HasOne(d => d.Author)
                .WithMany(u => u.Documents)
                .HasForeignKey(d => d.AuthorId)
                .OnDelete(DeleteBehavior.Restrict);

            // 6. Связь Document -> ApprovedBy
            modelBuilder.Entity<Document>()
                .HasOne(d => d.ApprovedBy)
                .WithMany()
                .HasForeignKey(d => d.ApprovedById)
                .OnDelete(DeleteBehavior.Restrict);

            // 7. Связь Task
            modelBuilder.Entity<Tasks>()
                .HasOne(t => t.AssignedTo)
                .WithMany(u => u.AssignedTasks)
                .HasForeignKey(t => t.AssignedToId)
                .OnDelete(DeleteBehavior.Restrict);

            modelBuilder.Entity<Tasks>()
                .HasOne(t => t.AssignedBy)
                .WithMany(u => u.CreatedTasks)
                .HasForeignKey(t => t.AssignedById)
                .OnDelete(DeleteBehavior.Restrict);

            // 8. Связь Workload -> Teacher
            modelBuilder.Entity<Workload>()
                .HasOne(w => w.Teacher)
                .WithMany(u => u.Workloads)
                .HasForeignKey(w => w.TeacherId)
                .OnDelete(DeleteBehavior.Restrict);

            // 9. Связь Workload -> Discipline
            modelBuilder.Entity<Workload>()
                .HasOne(w => w.Discipline)
                .WithMany(d => d.Workloads)
                .HasForeignKey(w => w.DisciplineId)
                .OnDelete(DeleteBehavior.Restrict);

            // 10. Связь Report -> CreatedBy
            modelBuilder.Entity<Report>()
                .HasOne(r => r.CreatedBy)
                .WithMany(u => u.Reports)
                .HasForeignKey(r => r.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            // 11. Связь AuditLog -> User
            modelBuilder.Entity<AuditLog>()
                .HasOne(a => a.User)
                .WithMany(u => u.AuditLogs)
                .HasForeignKey(a => a.UserId)
                .OnDelete(DeleteBehavior.Restrict);

            // 12. Связь Announcement -> CreatedBy
            modelBuilder.Entity<Announcement>()
                .HasOne(a => a.CreatedBy)
                .WithMany(u => u.Announcements)
                .HasForeignKey(a => a.CreatedById)
                .OnDelete(DeleteBehavior.Restrict);

            //// Seed данные (УБИРАЕМ Role, оставляем только Specialties)
            //modelBuilder.Entity<Specialty>().HasData(
            //    new Specialty { Id = 1, Name = "Информационные системы и программирование", Code = "09.02.07" },
            //    new Specialty { Id = 2, Name = "Обеспечение информационной безопасности автоматизированных систем", Code = "10.02.05" },
            //    new Specialty { Id = 3, Name = "Обеспечение информационной безопасности телекоммуникационных систем", Code = "10.02.04" },
            //    new Specialty { Id = 4, Name = "Интеллектуальные интегрированные системы", Code = "09.02.08" }
            //);
        }
    }
}