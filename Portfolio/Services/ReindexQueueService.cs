using System.Diagnostics;
using System.Threading.Channels;

namespace Portfolio.Services
{
    public interface IReindexManager
    {
        Task<string> EnqueueReindexAsync();
        ReindexStatus GetStatus();
    }

    public class ReindexStatus
    {
        public bool IsRunning { get; set; }
        public DateTime? LastRunAt { get; set; }
        public int LastCount { get; set; }
        public string? LastError { get; set; }
        public string? CurrentJobId { get; set; }
    }

    public class ReindexQueueService : BackgroundService, IReindexManager
    {
        private readonly Channel<string> _channel = Channel.CreateUnbounded<string>();
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReindexQueueService> _logger;
        private readonly ReindexStatus _status = new ReindexStatus();

        public ReindexQueueService(IServiceScopeFactory scopeFactory, ILogger<ReindexQueueService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        public ReindexStatus GetStatus() => _status;

        public async Task<string> EnqueueReindexAsync()
        {
            var id = Guid.NewGuid().ToString("N");
            await _channel.Writer.WriteAsync(id);
            _logger.LogInformation("Reindex job enqueued: {JobId}", id);
            return id;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await foreach (var jobId in _channel.Reader.ReadAllAsync(stoppingToken))
            {
                _status.IsRunning = true;
                _status.CurrentJobId = jobId;
                _status.LastError = null;
                _logger.LogInformation("Processing reindex job {JobId}", jobId);

                try
                {
                    var sw = Stopwatch.StartNew();
                    // create a scope for scoped services
                    using var scope = _scopeFactory.CreateScope();
                    var rag = scope.ServiceProvider.GetRequiredService<IRagService>();
                    await rag.RebuildIndexAsync();
                    var count = await rag.GetIndexCountAsync();
                    sw.Stop();
                    _status.LastRunAt = DateTime.UtcNow;
                    _status.LastCount = count;
                    _logger.LogInformation("Reindex job {JobId} completed: {Count} chunks in {Ms}ms", jobId, count, sw.ElapsedMilliseconds);
                }
                catch (Exception ex)
                {
                    _status.LastError = ex.Message;
                    _logger.LogError(ex, "Reindex job {JobId} failed", jobId);
                }
                finally
                {
                    _status.IsRunning = false;
                    _status.CurrentJobId = null;
                }
            }
        }
    }
}
