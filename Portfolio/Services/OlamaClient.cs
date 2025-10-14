using System.Text;
using System.Text.Json;
using Portfolio.Models;

namespace Portfolio.Services
{
    public class OlamaClient : ILocalModelClient
    {
        private readonly ILogger<OlamaClient> _logger;
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private readonly string? _url;
        private readonly TimeSpan _timeout;

        // Accept typed HttpClient so it matches AddHttpClient<OlamaClient> registration
        public OlamaClient(HttpClient http, IConfiguration config, ILogger<OlamaClient> logger)
        {
            _logger = logger;
            _config = config;
            _http = http;
            _url = _config["Olama:Url"];
            if (int.TryParse(_config["Olama:TimeoutSeconds"], out var s)) _timeout = TimeSpan.FromSeconds(s);
            else _timeout = TimeSpan.FromSeconds(30);
            _http.Timeout = _timeout;

            // If URL configured in settings, ensure BaseAddress is set (Program.cs may also set it)
            if (!string.IsNullOrWhiteSpace(_url))
            {
                try { _http.BaseAddress = new Uri(_url); } catch { }
            }
        }

        public async Task<string> GenerateAsync(string prompt, IList<Chat>? memory = null)
        {
            if (string.IsNullOrWhiteSpace(_url))
            {
                _logger.LogError("Olama:Url not configured");
                return string.Empty;
            }

            try
            {
                var payload = JsonSerializer.Serialize(new { prompt });
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync(_url, content);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Olama request failed: {Status} {Body}", (int)resp.StatusCode, body);
                    return string.Empty;
                }

                try
                {
                    using var doc = JsonDocument.Parse(body);
                    if (doc.RootElement.ValueKind == JsonValueKind.Object)
                    {
                        if (doc.RootElement.TryGetProperty("response", out var r)) return r.GetString() ?? string.Empty;
                        if (doc.RootElement.TryGetProperty("text", out var t)) return t.GetString() ?? string.Empty;
                    }
                }
                catch { }

                return body;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Olama call failed");
                return string.Empty;
            }
        }
    }
}
