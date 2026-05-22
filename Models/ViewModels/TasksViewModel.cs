using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;
using TaskStatus = PCKManagementSystem.Models.TaskStatus;

namespace PCKManagementSystem.Models.ViewModels
{
    public class TasksListViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Description { get; set; }
        public DateTime DueDate { get; set; }
        public string DueDateDisplay => DueDate.ToString("dd.MM.yyyy");
        public TaskStatus Status { get; set; }
        public string StatusDisplay
        {
            get
            {
                return Status switch
                {
                    TaskStatus.Pending => "Ожидает",
                    TaskStatus.InProgress => "В работе",
                    TaskStatus.Completed => "Выполнено",
                    TaskStatus.Overdue => "Просрочено",
                    TaskStatus.Cancelled => "Отменена",
                    _ => Status.ToString()
                };
            }
        }

        public string StatusClass
        {
            get
            {
                return Status switch
                {
                    TaskStatus.Pending => "bg-secondary",
                    TaskStatus.InProgress => "bg-primary",
                    TaskStatus.Completed => "bg-success",
                    TaskStatus.Overdue => "bg-danger",
                    TaskStatus.Cancelled => "bg-dark",
                    _ => "bg-secondary"
                };
            }
        }

        public string DisciplineName { get; set; }
        public string DisciplineCode { get; set; }

        public string AssignedToName { get; set; }
        public string AssignedByName { get; set; }
        public string? AttachmentFileName { get; set; }
        public string? AttachmentFilePath { get; set; }
        public string? AttachmentUrl { get; set; }

        public string? CompletionComment { get; set; }
        public string? CompletionAttachmentFilePath { get; set; }
        public string? CompletionAttachmentFileName { get; set; }
        public string? CompletionUrl { get; set; }
        public bool IsOverdue => Status != TaskStatus.Completed &&
                                  Status != TaskStatus.Cancelled &&
                                  DueDate < DateTime.UtcNow;

        public bool HasCompletionData => !string.IsNullOrEmpty(CompletionComment) || !string.IsNullOrEmpty(CompletionAttachmentFileName) 
            || !string.IsNullOrEmpty(CompletionUrl);
    }

    public class TasksCreateViewModel
    {
        [Required(ErrorMessage = "Введите название задачи")]
        [Display(Name = "Название задачи")]
        public string Title { get; set; }

        [Required(ErrorMessage = "Введите описание задачи")]
        [Display(Name = "Описание")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Укажите срок выполнения")]
        [Display(Name = "Срок выполнения")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; } = DateTime.UtcNow.AddDays(7);

        [Required(ErrorMessage = "Выберите дисциплину")]
        [Display(Name = "Дисциплина")]
        public int DisciplineId { get; set; }

        [Required(ErrorMessage = "Выберите исполнителя")]
        [Display(Name = "Исполнитель")]
        public int AssignedToId { get; set; }

        [Display(Name = "Прикрепить файл")]
        public IFormFile? AttachmentFile { get; set; }

        [Display(Name = "Ссылка на ресурс")]
        [Url(ErrorMessage = "Введите корректный URL")]
        public string? AttachmentUrl { get; set; }

        // Для выпадающих списков
        [ValidateNever]
        public List<SelectListItem> Disciplines { get; set; }
        [ValidateNever]
        public List<SelectListItem> Teachers { get; set; }
    }

    public class TasksEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Введите название задачи")]
        [Display(Name = "Название задачи")]
        public string Title { get; set; }

        [Display(Name = "Описание")]
        public string Description { get; set; }

        [Required(ErrorMessage = "Укажите срок выполнения")]
        [Display(Name = "Срок выполнения")]
        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        [Required(ErrorMessage = "Выберите дисциплину")]
        [Display(Name = "Дисциплина")]
        public int DisciplineId { get; set; }

        [Required(ErrorMessage = "Выберите исполнителя")]
        [Display(Name = "Исполнитель")]
        public int AssignedToId { get; set; }

        [Display(Name = "Связанный документ")]
        public int? DocumentId { get; set; }

        [Display(Name = "Статус")]
        public TaskStatus Status { get; set; }

        [Display(Name = "Ссылка на ресурс")]
        [Url(ErrorMessage = "Введите корректный URL")]
        public string? AttachmentUrl { get; set; }

        [Display(Name = "Текущий файл")]
        public string? ExistingAttachmentFileName { get; set; }
        public string? ExistingAttachmentFilePath { get; set; }

        [Display(Name = "Новый файл (для замены)")]
        public IFormFile? NewAttachmentFile { get; set; }

        [Display(Name = "Удалить текущий файл")]
        public bool RemoveAttachmentFile { get; set; }

        // Для выпадающих списков
        [ValidateNever]
        public List<SelectListItem> Disciplines { get; set; }
        [ValidateNever]
        public List<SelectListItem> Teachers { get; set; }
    }

    public class TasksFilterViewModel
    {
        public int? Status { get; set; }
        public int? DisciplineId { get; set; }
        public int? AssignedToId { get; set; }
        public bool? ShowOverdueOnly { get; set; }
        [ValidateNever]
        public List<SelectListItem> Statuses { get; set; }
        [ValidateNever]
        public List<SelectListItem> Disciplines { get; set; }
        [ValidateNever]
        public List<SelectListItem> Teachers { get; set; }
    }

    public class TasksChangeStatusViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public TaskStatus CurrentStatus { get; set; }

        [Required(ErrorMessage = "Выберите статус")]
        [Display(Name = "Новый статус")]
        public TaskStatus NewStatus { get; set; }

        [Display(Name = "Комментарий")]
        public string Comment { get; set; }
    }

    public class TaskCompletionViewModel
    {
        public int TaskId { get; set; }

        [Display(Name = "Комментарий о проделанной работе")]
        public string? Comment { get; set; }

        [Display(Name = "Прикрепить отчёт (файл)")]
        public IFormFile? AttachmentFile { get; set; }

        [Display(Name = "Ссылка на результат работы")]
        [Url(ErrorMessage = "Введите корректный URL")]
        public string? ResultUrl { get; set; }
    }
}