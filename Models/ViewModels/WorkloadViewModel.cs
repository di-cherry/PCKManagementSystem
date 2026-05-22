using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models.ViewModels
{
    public class WorkloadViewModel
    {
        public int Id { get; set; }
        public string TeacherName { get; set; }
        public string DisciplineName { get; set; }
        public string DisciplineCode { get; set; }
        public string AcademicYear { get; set; }
        public int Semester { get; set; }
        public int Hours { get; set; }
        public string LoadType { get; set; }
        public int GroupsCount { get; set; }
        public int AdditionalHours { get; set; } = 0;
        public string? Comments { get; set; }
        public DateTime CreatedAt { get; set; }
        public string? ControlType { get; set; }
        public int TotalHours { get; set; }      // итоговые часы
        public int? Course { get; set; }
        public string? StudyForm { get; set; }
    }

    public class WorkloadCreateViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Укажите преподавателя")]
        [Display(Name = "ФИО Преподавателя")]
        public int TeacherId { get; set; }

        [Required(ErrorMessage = "Укажите дисциплину")]
        [Display(Name = "Название дисциплины")]
        public int DisciplineId { get; set; }

        [Required(ErrorMessage = "Укажите учебный год")]
        [Display(Name = "Выберите год")]
        public string AcademicYear { get; set; }

        [Required(ErrorMessage = "Укажите семестр")]
        [Display(Name = "Выберите семестр")]
        public int Semester { get; set; }

        [Required(ErrorMessage = "Введите количество часов")]
        [Display(Name = "Кол часов")]
        public int Hours { get; set; }
        public int GroupsCount { get; set; } = 1;
        public string? Comments { get; set; }
        public string? ControlType { get; set; }
        public int TotalHours { get; set; }
        public int AdditionalHours { get; set; } = 0;

        [Required(ErrorMessage = "Введите курс дисциплины")]
        [Display(Name = "Курс")]
        [Range(1, 4, ErrorMessage = "Курс должен быть от 1 до 4")]
        public int? Course { get; set; }

        [Display(Name = "Форма обучения")]
        public string? StudyForm { get; set; }

        // Для выпадающих списков
        [ValidateNever]
        public List<SelectListItem>? Teachers { get; set; }
        [ValidateNever]
        public List<SelectListItem>? Disciplines { get; set; }


        // Список выбранных типов нагрузки (строки)

        public List<string> SelectedLoadTypes { get; set; } = new List<string>();

        // Для выпадающего списка: все возможные типы
        public List<SelectListItem> LoadTypeOptions { get; set; } = new List<SelectListItem>();
    }

    public class WorkloadFilterViewModel
    {
        public string? AcademicYear { get; set; }
        public int? Semester { get; set; }
        public int? TeacherId { get; set; }
        public int? DisciplineId { get; set; }
        [ValidateNever]
        public List<SelectListItem>? AcademicYears { get; set; }
        [ValidateNever]
        public List<SelectListItem>? Teachers { get; set; }
        [ValidateNever]
        public List<SelectListItem>? Disciplines { get; set; }
    }
}