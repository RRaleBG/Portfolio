using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Portfolio.Data;
using StackExchange.Redis;
using System.Diagnostics;
using System.Text;

using System.Text.Json;

namespace Portfolio.Services
{
    public partial class EmbeddingRagService : IRagService
    {
        private readonly IEmbeddingClient _embeddings;
        private readonly ApplicationDbContext _db;
        private readonly IWebHostEnvironment _env;
        private readonly IMemoryCache _cache;
        private readonly ILogger<EmbeddingRagService> _logger;
        private readonly IConnectionMultiplexer? _redis;
        private const string CacheKey = "rag_index_v1";
        private const string RedisKey = "rag:index:v1";
        private const int EmbeddingBatchSize = 64; // Safe default for most providers

        public EmbeddingRagService(
            IEmbeddingClient embeddings,
            ApplicationDbContext db,
            IWebHostEnvironment env,
            IMemoryCache cache,
            ILogger<EmbeddingRagService> logger,
            IConnectionMultiplexer? redis = null)
        {
            _embeddings = embeddings;
            _db = db;
            _env = env;
            _cache = cache;
            _logger = logger;
            _redis = redis;
        }

        // include Source label with each chunk
        private record Chunk(string Id, string Text, float[] Vector, string Source);

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        public async Task<string> BuildContextAsync(string userQuestion, int maxChars = 1500)
        {
            if (string.IsNullOrWhiteSpace(userQuestion)) return string.Empty;

            try
            {
                var chunks = await GetOrBuildIndexAsync();
                if (chunks.Count == 0) return string.Empty;

                var qv = await _embeddings.CreateEmbeddingAsync(userQuestion);

                var top = chunks
                    .Select(c => new { c.Text, c.Source, Score = CosineSimilarity(qv, c.Vector) })
                    .OrderByDescending(x => x.Score)
                    .Take(3)
                    .Select(x => $"Source: {x.Source}\n{x.Text}");

                var joined = string.Join("\n---\n", top);
                if (joined.Length > maxChars)
                    joined = joined.Substring(0, maxChars);
                return joined;
            }
            catch (HttpRequestException qx) when (qx.Data.Contains("QuotaExceeded") && qx.Data["QuotaExceeded"] is bool b && b)
            {
                _logger.LogWarning(qx, "RAG context build skipped due to quota; proceeding without context.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG context build failed; proceeding without context.");
                return string.Empty;
            }
        }

        public async Task<int> GetIndexCountAsync()
        {
            var list = await GetOrBuildIndexAsync();
            return list?.Count ?? 0;
        }

        public async Task RebuildIndexAsync()
        {
            // Remove cached index then build and return
            _cache.Remove(CacheKey);
            if (_redis != null) await _redis.GetDatabase().KeyDeleteAsync(RedisKey);
            await GetOrBuildIndexAsync();
        }

        // Public method used by background service to force build and return count
        public async Task<int> BuildIndexNowAsync()
        {
            _cache.Remove(CacheKey);
            if (_redis != null) await _redis.GetDatabase().KeyDeleteAsync(RedisKey);
            var list = await GetOrBuildIndexAsync();
            return list?.Count ?? 0;
        }

        private async Task<List<Chunk>> GetOrBuildIndexAsync()
        {
            try
            {
                if (_cache.TryGetValue(CacheKey, out List<Chunk> cached)) return cached;

                // If Redis available, try loading from Redis first
                if (_redis != null)
                {
                    try
                    {
                        var db = _redis.GetDatabase();
                        var raw = await db.StringGetAsync(RedisKey);
                        if (raw.HasValue)
                        {
                            var rawStr = raw.ToString();
                            var loaded = JsonSerializer.Deserialize<List<Chunk>>(rawStr, JsonOptions);
                            if (loaded != null && loaded.Count > 0)
                            {
                                _cache.Set(CacheKey, loaded, TimeSpan.FromHours(12));
                                _logger.LogInformation("[RAG] Loaded index from Redis: {Count} chunks", loaded.Count);
                                return loaded;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to load RAG index from Redis; continuing to build locally");
                    }
                }

                var sw = Stopwatch.StartNew();

                // store tuples of (text, source)
                var texts = new List<(string Text, string Source)>();

                var cvPath = Path.Combine(_env.WebRootPath, "data", "RajcicRados.pdf");
                _logger.LogDebug("[RAG] WebRoot: {WebRoot} | CV path: {CvPath} | Exists: {Exists}", _env.WebRootPath, cvPath, File.Exists(cvPath));
                // Prefer extracting directly from wwwroot/data/RajcicRados.pdf; if missing, fall back to cv.txt
                var pdfDataPath = Path.Combine(_env.WebRootPath, "data", "RajcicRados.pdf");
                try
                {
                    if (File.Exists(pdfDataPath))
                    {
                        _logger.LogInformation("[RAG] Extracting text from PDF in data: {PdfPath}", pdfDataPath);
                        var extracted = await Task.Run(() => ExtractTextFromPdf(pdfDataPath));
                        if (!string.IsNullOrWhiteSpace(extracted))
                        {
                            var chunks = Split(extracted, 800).ToList();
                            _logger.LogDebug("[RAG] PDF extracted: {CharCount} chars -> {ChunkCount} chunks", extracted.Length, chunks.Count);
                            foreach (var c in chunks) texts.Add((c, "CV"));
                        }
                    }
                    else if (File.Exists(cvPath))
                    {
                        var cv = await File.ReadAllTextAsync(cvPath);
                        var chunks = Split(cv, 800).ToList();
                        _logger.LogDebug("[RAG] cv.txt loaded: {CharCount} chars -> {ChunkCount} chunks", cv.Length, chunks.Count);
                        foreach (var c in chunks) texts.Add((c, "CV"));
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to extract CV text from data PDF or cv.txt");
                }

                var projs = await _db.Projects.AsNoTracking().ToListAsync();
                _logger.LogDebug("[RAG] Projects fetched: {Count}", projs.Count);
                foreach (var p in projs)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(p.Title);
                    if (!string.IsNullOrWhiteSpace(p.Summary)) sb.AppendLine(p.Summary);
                    if (!string.IsNullOrWhiteSpace(p.Tags)) sb.AppendLine($"Tags: {p.Tags}");
                    var chunks = Split(sb.ToString(), 800).ToList();
                    foreach (var c in chunks) texts.Add((c, $"Project: {p.Title}"));
                }

                var posts = await _db.BlogPosts.AsNoTracking().ToListAsync();
                _logger.LogDebug("[RAG] Blog posts fetched: {Count}", posts.Count);
                foreach (var b in posts)
                {
                    var sb = new StringBuilder();
                    sb.AppendLine(b.Title);
                    sb.AppendLine(b.Content);
                    var chunks = Split(sb.ToString(), 800).ToList();
                    foreach (var c in chunks) texts.Add((c, $"Blog: {b.Title}"));
                }

                var totalChars = texts.Sum(t => t.Text?.Length ?? 0);
                _logger.LogDebug("[RAG] Total text chunks: {ChunkCount} | Total chars: {TotalChars}", texts.Count, totalChars);

                if (texts.Count == 0)
                {
                    var ttl = _env.IsDevelopment() ? TimeSpan.FromMinutes(2) : TimeSpan.FromHours(12);
                    _logger.LogInformation("[RAG] No texts to index. Caching empty list for {TTL}", ttl);
                    _cache.Set(CacheKey, new List<Chunk>(), ttl);
                    return new List<Chunk>();
                }

                // Robust, batched embedding with bisect fallback on failures
                List<(int Index, float[] Vector)> embedded;
                try
                {
                    // convert to plain list of strings for embedding call
                    var plainTexts = texts.Select(t => t.Text).ToList();
                    embedded = await EmbedTextsRobustAsync(plainTexts, EmbeddingBatchSize);
                }
                catch (HttpRequestException qx) when (qx.Data.Contains("QuotaExceeded") && qx.Data["QuotaExceeded"] is bool b && b)
                {
                    // Prefer RetryAfter stored in exception Data
                    TimeSpan? retryAfter = null;
                    if (qx.Data.Contains("RetryAfter") && qx.Data["RetryAfter"] is TimeSpan ra) retryAfter = ra;

                    var ttl = retryAfter ?? (_env.IsDevelopment() ? TimeSpan.FromMinutes(2) : TimeSpan.FromMinutes(30));
                    _logger.LogWarning(qx, "[RAG] Quota exceeded during index build. Caching empty index for {TTL}", ttl);
                    _cache.Set(CacheKey, new List<Chunk>(), ttl);
                    return new List<Chunk>();
                }

                var successCount = embedded.Count;
                var skipped = texts.Count - successCount;
                if (successCount == 0)
                {
                    var ttl = _env.IsDevelopment() ? TimeSpan.FromMinutes(2) : TimeSpan.FromMinutes(30);
                    _logger.LogWarning("[RAG] All embeddings failed or were skipped. Caching empty index for {TTL}", ttl);
                    _cache.Set(CacheKey, new List<Chunk>(), ttl);
                    return new List<Chunk>();
                }

                // Build chunks preserving original text order for those successfully embedded
                var list = new List<Chunk>(successCount);
                foreach (var (Index, Vector) in embedded.OrderBy(e => e.Index))
                {
                    var source = texts[Index].Source;
                    var text = texts[Index].Text;
                    list.Add(new Chunk($"c{Index}", text, Vector, source));
                }

                _cache.Set(CacheKey, list, TimeSpan.FromHours(12));

                // Persist to Redis if available for durability
                if (_redis != null)
                {
                    try
                    {
                        var db = _redis.GetDatabase();
                        var json = JsonSerializer.Serialize(list, JsonOptions);
                        await db.StringSetAsync(RedisKey, json, TimeSpan.FromHours(24));
                        _logger.LogInformation("[RAG] Persisted index to Redis: {Count} chunks", list.Count);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to persist RAG index to Redis");
                    }
                }

                sw.Stop();
                _logger.LogInformation("[RAG] Index built: texts={Texts}, embedded={Embedded}, skipped={Skipped}, duration={Ms}ms, batchSize={Batch}",
                    texts.Count, successCount, skipped, sw.ElapsedMilliseconds, EmbeddingBatchSize);

                return list;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "RAG index build failed; caching empty index.");
                var ttl = _env.IsDevelopment() ? TimeSpan.FromMinutes(2) : TimeSpan.FromMinutes(30);
                _cache.Set(CacheKey, new List<Chunk>(), ttl);
                return new List<Chunk>();
            }
        }

        private static string ExtractTextFromPdf(string pdfPath)
        {
            try
            {
                // If PdfPig is not available, skip extraction
                var pdfPigType = Type.GetType("UglyToad.PdfPig.PdfDocument, UglyToad.PdfPig");
                if (pdfPigType == null) return string.Empty;

                // Dynamic call to PdfPig to avoid compile-time dependency
                dynamic doc = Activator.CreateInstance(pdfPigType, new object[] { pdfPath });
                var sb = new StringBuilder();
                foreach (var page in doc.GetPages())
                {
                    string text = page.Text as string;
                    if (!string.IsNullOrWhiteSpace(text)) sb.AppendLine(text);
                }
                return sb.ToString();
            }
            catch
            {
                return string.Empty;
            }
        }

        private async Task<List<(int Index, float[] Vector)>> EmbedTextsRobustAsync(List<string> texts, int batchSize)
        {
            var results = new List<(int Index, float[] Vector)>();

            async Task EmbedRangeAsync(int start, int count)
            {
                if (count <= 0) return;

                // If small enough, try as a single batch
                if (count <= batchSize)
                {
                    try
                    {
                        var inputs = texts.GetRange(start, count);
                        _logger.LogDebug("[RAG] Embedding batch start={Start} count={Count}", start, count);
                        var vectors = await _embeddings.CreateEmbeddingsAsync(inputs);

                        if (vectors == null)
                        {
                            _logger.LogWarning("[RAG] Embedding batch returned null vectors for range {Start}-{End}", start, start + count - 1);
                            return;
                        }

                        if (vectors.Count != inputs.Count)
                        {
                            var m = Math.Min(vectors.Count, inputs.Count);
                            _logger.LogWarning("[RAG] Embedding count mismatch for range {Start}-{End}: got {Got} expected {Expected}; taking first {Take}",
                                start, start + count - 1, vectors.Count, inputs.Count, m);
                            for (int i = 0; i < m; i++)
                                results.Add((start + i, vectors[i]));
                            return;
                        }

                        for (int i = 0; i < vectors.Count; i++)
                            results.Add((start + i, vectors[i]));
                        return;
                    }
                    catch (Exception ex)
                    {
                        if (count == 1)
                        {
                            _logger.LogWarning(ex, "[RAG] Skipping single item that failed to embed at index {Index}", start);
                            return;
                        }
                        _logger.LogWarning(ex, "[RAG] Embedding batch failed for range {Start}-{End}; bisecting", start, start + count - 1);
                        int left = count / 2;
                        int right = count - left;
                        await EmbedRangeAsync(start, left);
                        await EmbedRangeAsync(start + left, right);
                        return;
                    }
                }

                // Chunk into multiple batches when larger than batchSize
                int processed = 0;
                while (processed < count)
                {
                    int take = Math.Min(batchSize, count - processed);
                    await EmbedRangeAsync(start + processed, take);
                    processed += take;
                }
            }

            await EmbedRangeAsync(0, texts.Count);
            return results;
        }

        private static IEnumerable<string> Split(string text, int maxLen)
        {
            if (string.IsNullOrWhiteSpace(text)) yield break;
            for (int i = 0; i < text.Length; i += maxLen)
            {
                yield return text.Substring(i, Math.Min(maxLen, text.Length - i));
            }
        }

        private static float CosineSimilarity(IReadOnlyList<float> a, IReadOnlyList<float> b)
        {
            double dot = 0, na = 0, nb = 0;
            int len = Math.Min(a.Count, b.Count);
            for (int i = 0; i < len; i++)
            {
                dot += a[i] * b[i];
                na += a[i] * a[i];
                nb += b[i] * b[i];
            }
            return (float)(dot / (Math.Sqrt(na) * Math.Sqrt(nb) + 1e-8));
        }
    }

    public partial class EmbeddingRagService
    {
        public async Task<List<RagSnippetDto>> GetIndexedSnippetsAsync()
        {
            var chunks = await GetOrBuildIndexAsync();
            return chunks.Select(c => new RagSnippetDto { Id = c.Id, Source = c.Source, Text = c.Text }).ToList();
        }
    }
}