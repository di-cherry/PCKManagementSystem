using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models.ViewModels
{
    public class ProfileViewModel
    {
        public int Id { get; set; }
        public string Email { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? LastLoginAt { get; set; }
        public List<string> Roles { get; set; } = new();

        // Статистика
        public int DocumentsCreated { get; set; }
        public int DocumentsApproved { get; set; }
        public int TasksAssigned { get; set; }
        public int TasksCompleted { get; set; }
        public int TasksOverdue { get; set; }
        public int WorkloadHours { get; set; }
        [ValidateNever]
        public List<UserActivityLogViewModel> RecentActivities { get; set; } = new();
    }

    public class ProfileEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "Email обязателен")]
        [EmailAddress(ErrorMessage = "Введите корректный email")]
        [Display(Name = "Email")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите ФИО")]
        [Display(Name = "ФИО")]
        public string FullName { get; set; } = string.Empty;
    }

    public class ChangePasswordViewModel
    {
        [Required(ErrorMessage = "Введите текущий пароль")]
        [DataType(DataType.Password)]
        [Display(Name = "Текущий пароль")]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required(ErrorMessage = "Введите новый пароль")]
        [StringLength(100, ErrorMessage = "Пароль должен быть от {2} до {1} символов", MinimumLength = 4)]
        [DataType(DataType.Password)]
        [Display(Name = "Новый пароль")]
        public string NewPassword { get; set; } = string.Empty;

        [DataType(DataType.Password)]
        [Display(Name = "Подтверждение пароля")]
        [Compare("NewPassword", ErrorMessage = "Пароли не совпадают")]
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class UserActivityLogViewModel
    {
        public string ActionType { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string? EntityType { get; set; }
        public string? IpAddress { get; set; }
        public string? AdditionalInfo { get; set; }

        public string TimeAgo
        {
            get
            {
                var diff = DateTime.UtcNow - Date;
                if (diff.TotalMinutes < 1) return "только что";
                if (diff.TotalHours < 1) return $"{(int)diff.TotalMinutes} мин. назад";
                if (diff.TotalDays < 1) return $"{(int)diff.TotalHours} ч. назад";
                if (diff.TotalDays < 7) return $"{(int)diff.TotalDays} дн. назад";
                return Date.ToString("dd.MM.yyyy HH:mm");
            }
        }

        public string IconClass
        {
            get
            {
                return ActionType?.ToLower() switch
                {
                    "создание" => "bi-plus-circle text-success",
                    "редактирование" => "bi-pencil text-warning",
                    "удаление" => "bi-trash text-danger",
                    "вход" => "bi-box-arrow-in-right text-info",
                    "выход" => "bi-box-arrow-right text-secondary",
                    "блокировка" => "bi-lock text-dark",
                    "разблокировка" => "bi-unlock text-primary",
                    "назначение роли" => "bi-person-badge text-info",
                    _ => "bi-info-circle text-primary"
                };
            }
        }
    }

    public class UserActivityListViewModel
    {
        public List<UserActivityLogViewModel> Activities { get; set; } = new();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalItems { get; set; }
        public int TotalPages { get; set; }
    }
}