using MailKit.Net.Smtp;
using Microsoft.Extensions.Options;
using MimeKit;
using PCKManagementSystem.Hubs;

namespace PCKManagementSystem.Hubs
{
    public class EmailService : IEmailService
    {
        private readonly IConfiguration _configuration;

        public EmailService(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task SendEmailAsync(string toEmail, string subject, string message)
        {
            var emailSettings = _configuration.GetSection("EmailSettings");
            var smtpServer = emailSettings["SmtpServer"];
            var port = int.Parse(emailSettings["SmtpPort"]);
            var useSsl = bool.Parse(emailSettings["UseSsl"]);
            var senderEmail = emailSettings["SenderEmail"];
            var senderPassword = emailSettings["SenderPassword"];

            var email = new MimeMessage();
            email.From.Add(new MailboxAddress("АИС ПЦК", senderEmail));
            email.To.Add(new MailboxAddress("", toEmail));
            email.Subject = subject;

            var bodyBuilder = new BodyBuilder { HtmlBody = message };
            email.Body = bodyBuilder.ToMessageBody();

            using var smtp = new SmtpClient();
            await smtp.ConnectAsync(smtpServer, port, useSsl);
            await smtp.AuthenticateAsync(senderEmail, senderPassword);
            await smtp.SendAsync(email);
            await smtp.DisconnectAsync(true);
        }
    }
}