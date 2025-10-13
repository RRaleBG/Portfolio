using System.Threading.Tasks;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using MimeKit;
using Portfolio.Services;

namespace Portfolio.Services
{
    // Adapter to satisfy Identity UI generic email sender expectations
    public class IdentityEmailSender : IEmailSender<IdentityUser>
    {
        private readonly IMailSender _mailSender;
        private readonly IConfiguration _cfg;
        private string FromEmail => _cfg["EmailSettings:From"] ?? _cfg["EmailSettings:Username"] ?? "noreply@example.com";
        private string FromName => _cfg["Site:Name"] ?? "Rados AI";

        public IdentityEmailSender(IMailSender mailSender, IConfiguration cfg)
        {
            _mailSender = mailSender;
            _cfg = cfg;
        }

        public Task SendPasswordResetCodeAsync(IdentityUser user, string email, string resetCode)
        {
            var body = $"<p>Your password reset code is: <strong>{System.Net.WebUtility.HtmlEncode(resetCode)}</strong></p>";
            return SendAsync(email, "Password Reset Code", body);
        }

        public Task SendConfirmationLinkAsync(IdentityUser user, string email, string confirmationLink)
        {
            var body = $"<p>Confirm your account by clicking <a href='{confirmationLink}'>this link</a>.</p>";
            return SendAsync(email, "Confirm your email", body);
        }

        public Task SendPasswordResetLinkAsync(IdentityUser user, string email, string resetLink)
        {
            var body = $"<p>Reset your password by clicking <a href='{resetLink}'>this link</a>.</p>";
            return SendAsync(email, "Reset your password", body);
        }

        private async Task SendAsync(string toEmail, string subject, string html)
        {
            var msg = new MimeMessage();
            msg.From.Add(new MailboxAddress(FromName, FromEmail));
            msg.To.Add(new MailboxAddress(toEmail, toEmail));
            msg.Subject = subject;
            msg.Body = new TextPart("html") { Text = html };
            await _mailSender.SendAsync(msg);
        }
    }
}
