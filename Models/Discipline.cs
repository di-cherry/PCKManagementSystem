using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using System.ComponentModel.DataAnnotations;
using System.Reflection.Metadata;

namespace PCKManagementSystem.Models
{
    public class Discipline
    {
        public int Id { get; set; }
        [Required(ErrorMessage = "Введите название дисциплины")]
        [Display(Name = "Название дисциплины")]
        public string Name { get; set; }
        [Required(ErrorMessage = "Введите код дисциплины")]
        [Display(Name = "Код дисциплины")]
        public string Code { get; set; }

        [Required(ErrorMessage = "Укажите специальность дисциплины")]
        [Display(Name = "Специальность дисциплины")]
        public int SpecialtyId { get; set; }
        [ValidateNever]
        public Specialty? Specialty { get; set; }



        [ValidateNever]
        public ICollection<Document>? Documents { get; set; }

        [ValidateNever]
        public ICollection<Workload>? Workloads { get; set; }
    }
}
