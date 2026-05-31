using Microsoft.AspNetCore.Identity;
using PCKManagementSystem.Models;
using System.Threading.Tasks;

namespace PCKManagementSystem.Hubs
{
    public class IdentityEmailSender : IEmailSender<User>
    {
        private readonly IEmailSender _emailSender;

        public IdentityEmailSender(IEmailSender emailSender)
        {
            _emailSender = emailSender;
        }

        public async Task SendConfirmationLinkAsync(User user, string email, string confirmationLink)
        {
            await _emailSender.SendEmailAsync(email, "Подтверждение регистрации",
                $"Подтвердите регистрацию, перейдя по ссылке: <a href='{confirmationLink}'>Подтвердить</a>");
        }

        public async Task SendPasswordResetLinkAsync(User user, string email, string resetLink)
        {
            await _emailSender.SendEmailAsync(email, "Сброс пароля",
                $"Сбросьте пароль, перейдя по ссылке: <a href='{resetLink}'>Сбросить пароль</a>");
        }

        public async Task SendPasswordResetCodeAsync(User user, string email, string resetCode)
        {
            // Если используется код вместо ссылки
            await _emailSender.SendEmailAsync(email, "Код сброса пароля",
                $"Ваш код для сброса пароля: {resetCode}");
        }
    }
}