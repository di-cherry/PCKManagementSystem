using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models
{
    public class Workload
    {
        public int Id { get; set; }
        public int TeacherId { get; set; }
        public int DisciplineId { get; set; }
        public string AcademicYear { get; set; }
        public int Semester { get; set; }

        // Дополнительные часы (для консультаций, экзаменов и т.п.)
        public int AdditionalHours { get; set; } = 0;

        // Итоговые часы = Hours * GroupsCount + AdditionalHours
        public int TotalHours { get; set; }
        public int Hours { get; set; } // часы на одну группу
        public string? LoadType { get; set; }
        public int GroupsCount { get; set; } = 1;
        public string? Comments { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? UpdatedAt { get; set; }
        public string? ControlType { get; set; } // "Экзамен", "Зачёт", "Диф.зачёт", "Курсовая", "Нет"
        public int? Course { get; set; }
        public string? StudyForm { get; set; } // "Очная", "Заочная", "Очно-заочная"

        // Навигационные свойства
        [ValidateNever]
        public User Teacher { get; set; }
        [ValidateNever]
        public Discipline Discipline { get; set; }
    }
}
