using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models
{
    public class User : IdentityUser<int>
    {
        public string FullName { get; set; }
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        [ValidateNever]

        public ICollection<Document> Documents { get; set; }

        [ValidateNever]
        public ICollection<Workload> Workloads { get; set; }

        [ValidateNever]
        public ICollection<Report> Reports { get; set; }

        [ValidateNever]
        public ICollection<AuditLog> AuditLogs { get; set; }

        [ValidateNever]
        public ICollection<Tasks> AssignedTasks { get; set; }

        [ValidateNever]
        public ICollection<Tasks> CreatedTasks { get; set; }

        [ValidateNever]
        public ICollection<Announcement> Announcements { get; set; }

        // Профессиональные данные преподавателя
        [Display(Name = "Должность")]
        public string? Position { get; set; }

        [Display(Name = "Учёная степень (при наличии)")]
        public string? Degree { get; set; }

        [Display(Name = "Учёное звание (при наличии)")]
        public string? AcademicTitle { get; set; }

        [Display(Name = "Уровень профессионального образования")]
        public string? EducationLevel { get; set; }

        [Display(Name = "Квалификация")]
        public string? Qualification { get; set; }

        [Display(Name = "Сведения о повышении квалификации (за последние 3 года)")]
        public string? AdvancedTraining { get; set; }

        [Display(Name = "Сведения о профессиональной переподготовке")]
        public string? ProfessionalRetraining { get; set; }

        [Display(Name = "Опыт работы в профессиональной сфере (лет)")]
        public int? ExperienceYears { get; set; }

    }
}