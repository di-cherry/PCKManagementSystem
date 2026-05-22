using System.ComponentModel.DataAnnotations;

namespace PCKManagementSystem.Hubs
{
    public class AllowedEmailDomainAttribute : ValidationAttribute
    {
        private readonly string _allowedDomain;

        public AllowedEmailDomainAttribute(string allowedDomain)
        {
            _allowedDomain = allowedDomain;
        }

        protected override ValidationResult IsValid(object value, ValidationContext validationContext)
        {
            if (value is string email && !string.IsNullOrEmpty(email))
            {
                var domain = email.Split('@').Last();
                if (domain.Equals(_allowedDomain, StringComparison.OrdinalIgnoreCase))
                    return ValidationResult.Success;
                else
                    return new ValidationResult($"Допустимы только адреса {_allowedDomain}.");
            }
            return ValidationResult.Success;
        }
    }
}