using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models.ViewModels
{
    public class TeacherViewModel
    {
        public int Id { get; set; }
        public string FullName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string? Position { get; set; }
        public string? Degree { get; set; }
        public string? AcademicTitle { get; set; }
        public string? EducationLevel { get; set; }
        public string? Qualification { get; set; }
        public string? AdvancedTraining { get; set; }
        public string? ProfessionalRetraining { get; set; }
        public int? ExperienceYears { get; set; }
        public bool IsActive { get; set; }
    }

    public class TeacherEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Введите ФИО")]
        [Display(Name = "ФИО")]
        public string FullName { get; set; } = string.Empty;

        [EmailAddress]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Должность")]
        public string? Position { get; set; }

        [Display(Name = "Учёная степень")]
        public string? Degree { get; set; }

        [Display(Name = "Учёное звание")]
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

        [Display(Name = "Активен")]
        public bool IsActive { get; set; }
    }
}