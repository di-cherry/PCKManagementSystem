using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public string UserEmail { get; set; }
        public string UserFullName { get; set; }
        public string ActionType { get; set; }
        public string ActionDescription { get; set; }
        public string EntityType { get; set; }
        public int? EntityId { get; set; }
        [MaxLength(45)]
        public string IpAddress { get; set; }
        [MaxLength(500)]
        public string UserAgent { get; set; }
        public string OldValuesJson { get; set; }
        public string NewValuesJson { get; set; }
        public DateTime ActionDate { get; set; } = DateTime.UtcNow;
        public string AdditionalInfo { get; set; }

        // Навигационное свойство
        [ValidateNever]
        public User User { get; set; }
    }
}
