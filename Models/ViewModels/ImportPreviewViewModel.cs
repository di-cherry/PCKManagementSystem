namespace PCKManagementSystem.Models.ViewModels
{
    public class DisciplineImportPreviewViewModel
    {
        public int RowNumber { get; set; }
        public string Code { get; set; }
        public string Name { get; set; }
        public string SpecialtyCode { get; set; }
        public string SpecialtyName { get; set; }
        public bool IsValid { get; set; }
        public string ErrorMessage { get; set; }
        public bool IsNew { get; set; }  // true – новая дисциплина, false – обновление
        public int? ExistingDisciplineId { get; set; } // ID существующей дисциплины (если есть)
    }
}