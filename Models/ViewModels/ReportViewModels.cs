using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models.ViewModels
{
    #region Базовые модели для отчетов

    public class ReportParameterViewModel
    {
        [Display(Name = "Начальная дата")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "Конечная дата")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Учебный год")]
        public string? AcademicYear { get; set; }

        [Display(Name = "Семестр")]
        public int? Semester { get; set; }

        [Display(Name = "Преподаватель")]
        public int? TeacherId { get; set; }

        [Display(Name = "Дисциплина")]
        public int? DisciplineId { get; set; }

        [Display(Name = "Статус документа")]
        public DocumentStatus? DocumentStatus { get; set; }

        [Display(Name = "Тип отчета")]
        [Required(ErrorMessage = "Выберите тип отчета")]
        public ReportType ReportType { get; set; }

        [Display(Name = "Формат")]
        [Required(ErrorMessage = "Выберите формат")]
        public ReportFormat ReportFormat { get; set; } = ReportFormat.HTML;

        [Display(Name = "Курс")]
        public int? Course { get; set; }

        [Display(Name = "Форма обучения")]
        public string? StudyForm { get; set; }

        // Для выпадающих списков
        [ValidateNever]
        public List<SelectListItem>? AcademicYears { get; set; }
        [ValidateNever]
        public List<SelectListItem>? Teachers { get; set; }
        [ValidateNever]
        public List<SelectListItem>? Disciplines { get; set; }
        [ValidateNever]
        public List<SelectListItem>? DocumentStatuses { get; set; }
        [ValidateNever]
        public List<SelectListItem>? Courses { get; set; }
        [ValidateNever]
        public List<SelectListItem>? StudyForms { get; set; }

        // Поля для отображения (не участвуют в валидации, просто для сохранения в JSON)
        public string? TeacherName { get; set; }
        public string? DisciplineName { get; set; }
        public string? DocumentStatusName { get; set; }
        public string? PeriodDisplay { get; set; }
    }

    public enum ReportType
    {
        [Display(Name = "Отчет по документам")]
        Documents = 1,

        [Display(Name = "Отчет по учебной нагрузке")]
        Workload = 2,

        [Display(Name = "Отчет по задачам")]
        Tasks = 3,

        [Display(Name = "Отчет о деятельности ПЦК")]
        Activity = 4
    }

    public enum ReportFormat
    {
        [Display(Name = "HTML (просмотр)")]
        HTML = 1,

        [Display(Name = "Excel")]
        Excel = 2,

        [Display(Name = "CSV")]
        CSV = 3,

        [Display(Name = "PDF")]
        PDF = 4,

        [Display(Name = "Word")]
        Word = 5
    }

    #endregion

    #region Модели данных для отчетов

    // Отчет по документам
    public class DocumentsReportViewModel
    {
        public string Title { get; set; } = "Отчет по документам";
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public ReportParametersDisplay Parameters { get; set; } = new();

        // Общая статистика
        public int TotalDocuments { get; set; }
        public int DraftDocuments { get; set; }
        public int ReviewDocuments { get; set; }
        public int ApprovedDocuments { get; set; }
        public int RejectedDocuments { get; set; }

        // Статистика по статусам (для диаграммы)
        [ValidateNever]
        public Dictionary<string, int> DocumentsByStatus { get; set; } = new();

        // Статистика по дисциплинам
        [ValidateNever]
        public List<DocumentsByDisciplineViewModel> DocumentsByDiscipline { get; set; } = new();

        // Статистика по авторам
        [ValidateNever]
        public List<DocumentsByAuthorViewModel> DocumentsByAuthor { get; set; } = new();

        // Документы за период
        [ValidateNever]
        public List<DocumentItemViewModel> RecentDocuments { get; set; } = new();
    }

    public class DocumentsByDisciplineViewModel
    {
        public string DisciplineName { get; set; } = string.Empty;
        public string DisciplineCode { get; set; } = string.Empty;
        public int TotalDocuments { get; set; }
        public int ApprovedDocuments { get; set; }
        public int DraftDocuments { get; set; }
        public double ApprovalRate => TotalDocuments > 0 ? Math.Round((double)ApprovedDocuments / TotalDocuments * 100, 2) : 0;
    }

    public class DocumentsByAuthorViewModel
    {
        public string AuthorName { get; set; } = string.Empty;
        public int TotalDocuments { get; set; }
        public int ApprovedDocuments { get; set; }
        public int DraftDocuments { get; set; }
    }

    public class DocumentItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string DocumentType { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string AuthorName { get; set; } = string.Empty;
        public string DisciplineName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }

    // Отчет по нагрузке
    public class WorkloadReportViewModel
    {
        public string Title { get; set; } = "Отчет по учебной нагрузке";
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public ReportParametersDisplay Parameters { get; set; } = new();

        // Общая статистика
        public int TotalTeachers { get; set; }
        public int TotalDisciplines { get; set; }
        public int TotalHours { get; set; }
        public int TotalGroups { get; set; }


        // Статистика по типам нагрузки
        [ValidateNever]
        public Dictionary<string, int> HoursByLoadType { get; set; } = new();

        // Нагрузка по преподавателям
        [ValidateNever]
        public List<WorkloadByTeacherViewModel> WorkloadByTeacher { get; set; } = new();

        // Нагрузка по дисциплинам
        [ValidateNever]
        public List<WorkloadByDisciplineViewModel> WorkloadByDiscipline { get; set; } = new();

        // Детальная нагрузка
        [ValidateNever]
        public List<WorkloadItemViewModel> WorkloadDetails { get; set; } = new();
    }

    public class WorkloadByTeacherViewModel
    {
        public string TeacherName { get; set; } = string.Empty;
        public int TotalHours { get; set; }
        public int DisciplinesCount { get; set; }
        public Dictionary<string, int> HoursByType { get; set; } = new();
    }

    public class WorkloadByDisciplineViewModel
    {
        public string DisciplineName { get; set; } = string.Empty;
        public string DisciplineCode { get; set; } = string.Empty;
        public int TotalHours { get; set; }
        public int TeachersCount { get; set; }
    }

    public class WorkloadItemViewModel
    {
        public int Id { get; set; }
        public string TeacherName { get; set; } = string.Empty;
        public string DisciplineName { get; set; } = string.Empty;
        public string DisciplineCode { get; set; } = string.Empty;
        public string AcademicYear { get; set; } = string.Empty;
        public int Semester { get; set; }
        public int Hours { get; set; }
        public string LoadType { get; set; } = string.Empty;
        public int GroupsCount { get; set; }
        public string Comments { get; set; } = string.Empty;
        public string ControlType { get; set; } = string.Empty;
    }

    // Отчет по задачам
    public class TasksReportViewModel
    {
        public string Title { get; set; } = "Отчет по задачам";
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public ReportParametersDisplay Parameters { get; set; } = new();

        // Общая статистика
        public int TotalTasks { get; set; }
        public int PendingTasks { get; set; }
        public int InProgressTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int CancelledTasks { get; set; }

        // Статистика по статусам
        [ValidateNever]
        public Dictionary<string, int> TasksByStatus { get; set; } = new();

        // Задачи по исполнителям
        [ValidateNever]
        public List<TasksByExecutorViewModel> TasksByExecutor { get; set; } = new();

        // Просроченные задачи
        [ValidateNever]
        public List<TaskItemViewModel> OverdueTasksList { get; set; } = new();

        // Последние задачи
        [ValidateNever]
        public List<TaskItemViewModel> RecentTasks { get; set; } = new();
    }

    public class TasksByExecutorViewModel
    {
        public string ExecutorName { get; set; } = string.Empty;
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }
        public int OverdueTasks { get; set; }
        public double CompletionRate => TotalTasks > 0 ? Math.Round((double)CompletedTasks / TotalTasks * 100, 2) : 0;
    }

    public class TaskItemViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime DueDate { get; set; }
        public string Status { get; set; } = string.Empty;
        public string ExecutorName { get; set; } = string.Empty;
        public string CreatorName { get; set; } = string.Empty;
        public string DisciplineName { get; set; } = string.Empty;
        public bool IsOverdue => DueDate < DateTime.UtcNow && Status != "Completed" && Status != "Cancelled";
    }

    // Общий отчет о деятельности
    public class ActivityReportViewModel
    {
        public string Title { get; set; } = "Отчет о деятельности ПЦК";
        public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
        public ReportParametersDisplay Parameters { get; set; } = new();
        public string Period { get; set; } = string.Empty;

        // Сводная статистика
        public int TotalDocuments { get; set; }
        public int ApprovedDocuments { get; set; }
        public int TotalWorkloadHours { get; set; }
        public int TotalTasks { get; set; }
        public int CompletedTasks { get; set; }

        // Активность преподавателей
        [ValidateNever]
        public List<TeacherActivityViewModel> TeachersActivity { get; set; } = new();

        // Динамика по месяцам
        [ValidateNever]
        public List<MonthlyActivityViewModel> MonthlyActivity { get; set; } = new();
    }

    public class TeacherActivityViewModel
    {
        public string TeacherName { get; set; } = string.Empty;
        public int DocumentsCreated { get; set; }
        public int DocumentsApproved { get; set; }
        public int WorkloadHours { get; set; }
        public int TasksAssigned { get; set; }
        public int TasksCompleted { get; set; }
    }

    public class MonthlyActivityViewModel
    {
        public string Month { get; set; } = string.Empty;
        public int Year { get; set; }
        public int DocumentsCreated { get; set; }
        public int TasksCreated { get; set; }
        public int TasksCompleted { get; set; }
    }

    public class ReportParametersDisplay
    {
        public string? Period { get; set; }
        public string? AcademicYear { get; set; }
        public int? Semester { get; set; }
        public string? Teacher { get; set; }
        public string? Discipline { get; set; }
        public string? DocumentStatus { get; set; }

        public bool HasParameters => !string.IsNullOrEmpty(Period) ||
                                     !string.IsNullOrEmpty(AcademicYear) ||
                                     Semester.HasValue ||
                                     !string.IsNullOrEmpty(Teacher) ||
                                     !string.IsNullOrEmpty(Discipline) ||
                                     !string.IsNullOrEmpty(DocumentStatus);
    }

    // Для списка сохраненных отчетов
    public class SavedReportViewModel
    {
        public int Id { get; set; }
        public string Title { get; set; } = string.Empty;
        public string ReportType { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public string CreatedBy { get; set; } = string.Empty;
        public string Format { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool HasFile => !string.IsNullOrEmpty(FilePath);
    }

    #endregion
}