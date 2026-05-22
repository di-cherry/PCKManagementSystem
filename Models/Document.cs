using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models
{
    public class Document
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Введите название документа")]
        [Display(Name = "Название документа")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Укажите тип документа")]
        [Display(Name = "Тип документа")]
        public string DocumentType { get; set; }
        [Required(ErrorMessage = "Укажите версию документа")]
        [Display(Name = "Версию документа")]
        public string Version { get; set; }
        public DateTime CreatedAt { get; set; }
        [ValidateNever]
        public string FilePath { get; set; }
        [Required(ErrorMessage = "Укажите дисциплину документа")]
        [Display(Name = "Дисциплину документа")]
        public int DisciplineId { get; set; }
        public int AuthorId { get; set; }

        // Новые поля для workflow
        public DocumentStatus Status { get; set; } = DocumentStatus.Draft;
        public DateTime? ApprovedAt { get; set; }
        public int? ApprovedById { get; set; }
        public string? RejectionReason { get; set; }

        // Навигационные свойства
        [ValidateNever]
        public Discipline? Discipline { get; set; }

        [ValidateNever]
        public User? Author { get; set; }

        [ValidateNever]
        public User? ApprovedBy { get; set; }
    }
    public enum DocumentStatus
    {
        Draft,      // Черновик
        Review,     // На рассмотрении
        Approved,   // Утвержден
        Rejected    // Отклонен
    }
}
