using MimeKit;

namespace Portfolio.Services
{
    public interface IMailSender
    {
        Task SendAsync(MimeMessage message);
    }
}
