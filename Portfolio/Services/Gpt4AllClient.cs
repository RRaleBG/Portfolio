using Portfolio.Models;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

namespace Portfolio.Services
{
    public class Gpt4AllClient : ILocalModelClient
    {
        private readonly ILogger<Gpt4AllClient> _logger;
        private readonly IConfiguration _config;
        private readonly HttpClient _http;
        private readonly string _mode;
        private readonly string? _cliPath;
        private readonly string? _cliArgs;
        private readonly string? _url;
        private readonly TimeSpan _timeout;

        public Gpt4AllClient(HttpClient http, IConfiguration config, ILogger<Gpt4AllClient> logger)
        {
            _logger = logger;
            _config = config;
            _http = http;

            _mode = (_config["Gpt4All:Mode"] ?? "cli").ToLowerInvariant();
            _cliPath = _config["Gpt4All:CliPath"];
            _cliArgs = _config["Gpt4All:CliArgs"];
            _url = _config["Gpt4All:Url"];

            if (int.TryParse(_config["Gpt4All:TimeoutSeconds"], out var s)) _timeout = TimeSpan.FromSeconds(s);
            else _timeout = TimeSpan.FromSeconds(30);

            _http.Timeout = _timeout;
        }

        public async Task<string> GenerateAsync(string prompt, IList<Chat>? memory = null)
        {
            if (string.IsNullOrWhiteSpace(prompt)) return string.Empty;

            try
            {
                // HTTP mode explicit
                if (_mode == "http")
                {
                    if (string.IsNullOrWhiteSpace(_url))
                    {
                        _logger.LogError("Gpt4All HTTP mode selected but Gpt4All:Url is not configured.");
                        return string.Empty;
                    }

                    var payload = JsonSerializer.Serialize(new { prompt });
                    using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                    var resp = await _http.PostAsync(_url, content);
                    var body = await resp.Content.ReadAsStringAsync();
                    if (!resp.IsSuccessStatusCode)
                    {
                        _logger.LogWarning("Gpt4All HTTP request failed: {Status} {Body}", (int)resp.StatusCode, body);
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

                // CLI mode (or default) - validate binary exists before attempting to start
                if (string.IsNullOrWhiteSpace(_cliPath))
                {
                    _logger.LogError("Gpt4All CLI mode selected but Gpt4All:CliPath is not configured.");
                    // fallback to HTTP if available
                    if (!string.IsNullOrWhiteSpace(_url))
                    {
                        _logger.LogInformation("Attempting HTTP fallback because CLI path is missing.");
                        return await TryHttpFallback(prompt);
                    }
                    return string.Empty;
                }

                if (!System.IO.File.Exists(_cliPath))
                {
                    _logger.LogWarning("Gpt4All CLI executable not found at '{Path}'.", _cliPath);
                    if (!string.IsNullOrWhiteSpace(_url))
                    {
                        _logger.LogInformation("Attempting HTTP fallback because CLI executable is missing.");
                        return await TryHttpFallback(prompt);
                    }
                    return string.Empty;
                }

                var psi = new ProcessStartInfo
                {
                    FileName = _cliPath,
                    Arguments = _cliArgs ?? string.Empty,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };

                using var proc = new Process { StartInfo = psi };
                try
                {
                    proc.Start();
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to start Gpt4All CLI process at '{Path}'.", _cliPath);
                    if (!string.IsNullOrWhiteSpace(_url))
                    {
                        _logger.LogInformation("Attempting HTTP fallback because starting CLI failed.");
                        return await TryHttpFallback(prompt);
                    }
                    return string.Empty;
                }

                // write prompt and close stdin
                await proc.StandardInput.WriteAsync(prompt);
                proc.StandardInput.Close();

                // read output with timeout
                var outputTask = proc.StandardOutput.ReadToEndAsync();
                var errorTask = proc.StandardError.ReadToEndAsync();

                var completed = await Task.WhenAny(Task.WhenAll(outputTask, errorTask), Task.Delay(_timeout));
                if (completed is Task delay && delay.IsCompleted && !Task.WhenAll(outputTask, errorTask).IsCompleted)
                {
                    try { proc.Kill(); } catch { }
                    _logger.LogWarning("Gpt4All CLI process timed out after {Timeout}s", _timeout.TotalSeconds);
                    return string.Empty;
                }

                var output = await outputTask;
                var error = await errorTask;
                if (!string.IsNullOrWhiteSpace(error)) _logger.LogDebug("Gpt4All CLI stderr: {Error}", error);

                return output.Trim();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Gpt4All generation failed");
                return string.Empty;
            }
        }

        private async Task<string> TryHttpFallback(string prompt)
        {
            if (string.IsNullOrWhiteSpace(_url)) return string.Empty;
            try
            {
                var payload = JsonSerializer.Serialize(new { prompt });
                using var content = new StringContent(payload, Encoding.UTF8, "application/json");
                var resp = await _http.PostAsync(_url, content);
                var body = await resp.Content.ReadAsStringAsync();
                if (!resp.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Gpt4All HTTP fallback request failed: {Status} {Body}", (int)resp.StatusCode, body);
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
                _logger.LogWarning(ex, "Gpt4All HTTP fallback failed");
                return string.Empty;
            }
        }
    }
}
