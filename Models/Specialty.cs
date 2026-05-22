using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Models
{
    public class Specialty
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Введите название специальности")]
        [Display(Name = "Название специальности")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Введите код специальности")]
        [Display(Name = "Код специальности")]
        public string Code { get; set; }

        [ValidateNever]
        public ICollection<Discipline> Disciplines { get; set; }
    }
}
