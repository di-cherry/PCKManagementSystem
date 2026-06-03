using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PCKManagementSystem.Models
{
    public class Tasks
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime DueDate { get; set; }
        public TaskStatus Status { get; set; } = TaskStatus.Pending;

        public int DisciplineId { get; set; }
        public int AssignedToId { get; set; }  // Кому
        public int AssignedById { get; set; }  // Кто поставил
        
        // Новое поле для прикрепления файла
        public string? AttachmentFilePath { get; set; }
        public string? AttachmentFileName { get; set; }  // оригинальное имя для отображения

        // Новое поле для ссылки
        public string? AttachmentUrl { get; set; }

        public string? CompletionComment { get; set; }
        public string? CompletionAttachmentFilePath { get; set; }
        public string? CompletionAttachmentFileName { get; set; }
        public string? CompletionUrl { get; set; }

        [ValidateNever]
        public Discipline Discipline { get; set; }
        [ValidateNever]
        public User AssignedTo { get; set; }
        [ValidateNever]
        public User AssignedBy { get; set; }
    }
    public enum TaskStatus
    {
        Pending = 0,
        InProgress = 1,
        Completed = 2,
        Overdue = 3,
        Cancelled = 4
    }
}
