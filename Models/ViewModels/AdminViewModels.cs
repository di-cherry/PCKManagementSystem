using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models.ViewModels
{
    #region Управление пользователями

    public class UserListViewModel
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public List<string> Roles { get; set; } = new();
        public string StatusDisplay => IsActive ? "Активен" : "Заблокирован";
        public string StatusClass => IsActive ? "bg-success" : "bg-danger";
    }

    public class UserEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Введите email")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Display(Name = "Имя пользователя")]
        public string UserName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите ФИО пользователя")]
        [Display(Name = "ФИО")]
        public string FullName { get; set; } = string.Empty;

        [Display(Name = "Активен")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Роли")]
        public List<string> SelectedRoles { get; set; } = new();

        // Для выпадающего списка
        [ValidateNever]
        public List<SelectListItem> AllRoles { get; set; } = new();
    }

    public class UserCreateViewModel
    {
        [Required(ErrorMessage = "Введите email")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите ФИО пользователя")]
        [Display(Name = "ФИО")]
        public string FullName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль")]
        [StringLength(100, ErrorMessage = "Пароль должен быть от {2} до {1} символов", MinimumLength = 4)]
        [DataType(DataType.Password)]
        [Display(Name = "Пароль")]
        public string Password { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите пароль повторно")]
        [DataType(DataType.Password)]
        [Display(Name = "Подтверждение пароля")]
        [Compare("Password", ErrorMessage = "Пароли не совпадают")]
        public string ConfirmPassword { get; set; } = string.Empty;

        [Display(Name = "Активен")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "Роли")]
        public List<string> SelectedRoles { get; set; } = new();

        // Для выпадающего списка
        [ValidateNever]
        public List<SelectListItem> AllRoles { get; set; } = new();
    }

    #endregion

    #region Журнал аудита

    public class AuditLogViewModel
    {
        public int Id { get; set; }
        public string UserEmail { get; set; } = string.Empty;
        public string UserFullName { get; set; } = string.Empty;
        public string ActionType { get; set; } = string.Empty;
        public string ActionDescription { get; set; } = string.Empty;
        public string EntityType { get; set; } = string.Empty;
        public int? EntityId { get; set; }
        public string IpAddress { get; set; } = string.Empty;
        public DateTime ActionDate { get; set; }
        public string AdditionalInfo { get; set; } = string.Empty;

        // Форматированное отображение
        public string TimeAgo
        {
            get
            {
                var diff = DateTime.UtcNow - ActionDate;
                if (diff.TotalMinutes < 1) return "только что";
                if (diff.TotalHours < 1) return $"{diff.Minutes} мин. назад";
                if (diff.TotalDays < 1) return $"{diff.Hours} ч. назад";
                if (diff.TotalDays < 7) return $"{diff.Days} дн. назад";
                return ActionDate.ToString("dd.MM.yyyy HH:mm");
            }
        }
    }

    public class AuditLogFilterViewModel
    {
        [Display(Name = "Тип действия")]
        public string? ActionType { get; set; }

        [Display(Name = "Тип сущности")]
        public string? EntityType { get; set; }

        [Display(Name = "Пользователь")]
        public int? UserId { get; set; }

        [Display(Name = "Начальная дата")]
        [DataType(DataType.Date)]
        public DateTime? StartDate { get; set; }

        [Display(Name = "Конечная дата")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "Поиск")]
        public string? SearchTerm { get; set; }

        // Для выпадающих списков
        [ValidateNever]
        public List<SelectListItem>? ActionTypes { get; set; }
        [ValidateNever]
        public List<SelectListItem>? EntityTypes { get; set; }
        [ValidateNever]
        public List<SelectListItem>? Users { get; set; }
    }

    #endregion

    #region Объявления

    public class AnnouncementViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Введите заголовок")]
        [Display(Name = "Заголовок")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите содержание")]
        [Display(Name = "Содержание")]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "Активно")]
        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
        public int CreatedById { get; set; }
    }

    public class AnnouncementCreateViewModel
    {
        [Required(ErrorMessage = "Введите заголовок")]
        [Display(Name = "Заголовок")]
        public string Title { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите содержание")]
        [Display(Name = "Содержание")]
        public string Content { get; set; } = string.Empty;

        [Display(Name = "Активно")]
        public bool IsActive { get; set; } = true;
    }

    #endregion

    #region Статистика

    public class DashboardStatisticsViewModel
    {
        // Общая статистика
        public int TotalUsers { get; set; }
        public int ActiveUsers { get; set; }
        public int TeachersCount { get; set; }
        public int ChairmenCount { get; set; }
        public int AdminsCount { get; set; }

        public int TotalDocuments { get; set; }
        public int DraftDocuments { get; set; }
        public int ReviewDocuments { get; set; }
        public int ApprovedDocuments { get; set; }
        public int RejectedDocuments { get; set; }

        public int TotalDisciplines { get; set; }
        public int TotalSpecialties { get; set; }

        public int TotalWorkloadHours { get; set; }
        public int TotalWorkloadRecords { get; set; }

        public int TotalTasks { get; set; }
        public int PendingTasks { get; set; }
        public int OverdueTasks { get; set; }
        public int CompletedTasks { get; set; }

        // Графики и диаграммы
        [ValidateNever]
        public Dictionary<string, int> DocumentsByStatus { get; set; } = new();
        [ValidateNever]
        public Dictionary<string, int> TasksByStatus { get; set; } = new();
        [ValidateNever]
        public List<UserActivityViewModel> MostActiveUsers { get; set; } = new();
        [ValidateNever]
        public List<RecentActivityViewModel> RecentActivities { get; set; } = new();
    }

    public class UserActivityViewModel
    {
        public string UserName { get; set; } = string.Empty;
        public string UserEmail { get; set; } = string.Empty;
        public int DocumentsCreated { get; set; }
        public int TasksAssigned { get; set; }
        public int TasksCompleted { get; set; }
        public int TotalActions => DocumentsCreated + TasksAssigned + TasksCompleted;
    }

    public class RecentActivityViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public DateTime Time { get; set; }
        public string Icon { get; set; } = "bi-info-circle";
        public string Color { get; set; } = "primary";

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.UtcNow - Time;
                if (diff.TotalMinutes < 1) return "только что";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} мин. назад";
                if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} ч. назад";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} дн. назад";
                return Time.ToString("dd.MM.yyyy HH:mm");
            }
        }
    }

    #endregion
}