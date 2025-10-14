using Microsoft.Extensions.Caching.Memory;

namespace Portfolio.Services
{
    // Very simple RAG: picks lines containing any keywords
    public class KeywordRagService(IWebHostEnvironment env, IMemoryCache cache)
    {
        private readonly IWebHostEnvironment _env = env;
        private readonly IMemoryCache _cache = cache;

        public string GetCvText()
        {
            var path = Path.Combine(_env.WebRootPath, "data", "RajcicRados.pdf");
            if (!File.Exists(path)) return string.Empty;
            return File.ReadAllText(path);
        }

        public string GetProjectsText()
        {
            var path = Path.Combine(_env.WebRootPath, "data", "projects.json");
            if (!File.Exists(path)) return string.Empty;
            return File.ReadAllText(path);
        }

        public string BuildContext(string userQuestion, string corpus, int maxChars = 1500)
        {
            if (string.IsNullOrWhiteSpace(corpus)) 
                return string.Empty;

            var words = userQuestion.Split(new[] { ' ', '\t', '\r', '\n', ',', '.', ';', ':', '!' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                    .Select(w => w.ToLowerInvariant())
                                    .Distinct()
                                    .ToHashSet();

            var lines = corpus.Split('\n');
            var matches = new List<string>();

            foreach (var line in lines)
            {
                var l = line.Trim();

                if (l.Length == 0) 
                    continue;

                var lower = l.ToLowerInvariant();
                if (words.Any(w => lower.Contains(w)))
                {
                    matches.Add(l);
                    if (matches.Sum(x => x.Length) > maxChars) 
                        break;
                }
            }

            if (matches.Count == 0)
            {
                // fallback: first N chars
                return corpus.Substring(0, Math.Min(maxChars, corpus.Length));
            }
            return string.Join("\n", matches);
        }
    }
}
