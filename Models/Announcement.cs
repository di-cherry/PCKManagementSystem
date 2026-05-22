using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace PCKManagementSystem.Models
{
    public class Announcement
    {
        public int Id { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsActive { get; set; } = true;

        public int CreatedById { get; set; }
        [ValidateNever]
        public User CreatedBy { get; set; }
    }
}
