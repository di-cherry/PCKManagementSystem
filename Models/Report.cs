using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models
{
    public class Report
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string ReportType { get; set; }
        public string Period { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public int CreatedById { get; set; }
        public string? FilePath { get; set; }
        public string? ReportDataJson { get; set; }
        [MaxLength(20)]
        public string Format { get; set; } = "HTML";
        public ReportStatus Status { get; set; } = ReportStatus.Generated;
        public string ParametersJson { get; set; }

        // Навигационные свойства
        [ValidateNever]
        public User CreatedBy { get; set; }
    }
    public enum ReportStatus
    {
        Generated,      // Сгенерирован
        Sent,           // Отправлен
        Archived,       // В архиве
        Error           // Ошибка генерации
    }
}
