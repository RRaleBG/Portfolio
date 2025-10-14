namespace Portfolio.Services
{
    public class ChatMetricsService
    {
        private long _totalRequests;
        private long _successes;
        private long _errors;
        private long _totalTicks; // cumulative ticks for timing

        public void RecordRequest()
        {
            Interlocked.Increment(ref _totalRequests);
        }

        public void RecordSuccess(TimeSpan duration)
        {
            Interlocked.Increment(ref _successes);
            Interlocked.Add(ref _totalTicks, duration.Ticks);
        }

        public void RecordError(TimeSpan duration)
        {
            Interlocked.Increment(ref _errors);
            Interlocked.Add(ref _totalTicks, duration.Ticks);
        }

        public ChatMetricsSnapshot GetSnapshot()
        {
            var total = Interlocked.Read(ref _totalRequests);
            var succ = Interlocked.Read(ref _successes);
            var err = Interlocked.Read(ref _errors);
            var ticks = Interlocked.Read(ref _totalTicks);
            double avgMs = 0;
            var counted = succ + err;
            if (counted > 0) avgMs = new TimeSpan(ticks / counted).TotalMilliseconds;

            return new ChatMetricsSnapshot(total, succ, err, avgMs);
        }
    }

    public record ChatMetricsSnapshot(long TotalRequests, long Successes, long Errors, double AverageResponseMs);
}
