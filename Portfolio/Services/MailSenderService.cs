using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace Portfolio.Services
{
    public class MailSenderService : IMailSender
    {
        private readonly IConfiguration _config;
        private readonly ILogger<MailSenderService> _logger;
        public MailSenderService(IConfiguration config, ILogger<MailSenderService> logger)
        {
            _config = config;
            _logger = logger;
        }

        public async Task SendAsync(MimeMessage message)
        {
            // Read SMTP from EmailSettings section directly
            var host = _config["EmailSettings:SmtpHost"] ?? "localhost";
            var port = int.TryParse(_config["EmailSettings:SmtpPort"], out var p) ? p : 25;
            var username = _config["EmailSettings:Username"] ?? string.Empty;
            var password = _config["EmailSettings:Password"] ?? string.Empty;
            var enableSsl = bool.TryParse(_config["EmailSettings:EnableSsl"], out var es) && es;
            var secure = _config["EmailSettings:Secure"] ?? (enableSsl ? "StartTls" : "None");
            var options = secure switch
            {
                "None" => SecureSocketOptions.None,
                "SslOnConnect" => SecureSocketOptions.SslOnConnect,
                "StartTlsWhenAvailable" => SecureSocketOptions.StartTlsWhenAvailable,
                _ => SecureSocketOptions.StartTls
            };

            using var smtp = new SmtpClient();
            try
            {
                await smtp.ConnectAsync(host, port, options);

                if (!string.IsNullOrEmpty(username))
                {
                    await smtp.AuthenticateAsync(username, password);
                }
                await smtp.SendAsync(message);
            }
            finally
            {
                await smtp.DisconnectAsync(true);
            }
        }
    }
}
