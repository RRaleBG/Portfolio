using MimeKit;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Portfolio.Services
{
    public class MailQueueService : BackgroundService
    {
        private readonly IMailSender _mailSender;
        private readonly ILogger<MailQueueService> _logger;
        private readonly ConcurrentQueue<MimeMessage> _queue = new();
        private readonly SemaphoreSlim _signal = new(0);

        public MailQueueService(IMailSender mailSender, ILogger<MailQueueService> logger)
        {
            _mailSender = mailSender;
            _logger = logger;
        }

        public void Enqueue(MimeMessage message)
        {
            _queue.Enqueue(message);
            _signal.Release();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("MailQueueService started.");
            while (!stoppingToken.IsCancellationRequested)
            {
                await _signal.WaitAsync(stoppingToken);
                if (_queue.TryDequeue(out var message))
                {
                    try
                    {
                        await _mailSender.SendAsync(message);
                        _logger.LogInformation("Email sent to {To} with subject '{Subject}'.", string.Join(", ", message.To), message.Subject);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to send email to {To} with subject '{Subject}'.", string.Join(", ", message.To), message.Subject);
                    }
                }
            }
            _logger.LogInformation("MailQueueService stopping.");
        }
    }
}
