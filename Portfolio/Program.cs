using JavaScriptEngineSwitcher.Extensions.MsDependencyInjection;
using JavaScriptEngineSwitcher.V8;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Portfolio.Database;
using Portfolio.Helpers;
using Portfolio.Hubs;
using Portfolio.Models;
using Portfolio.Services;
using StackExchange.Redis;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddJsEngineSwitcher(o => o.DefaultEngineName = V8JsEngine.EngineName)
    .AddV8();

// Add services to the container.
builder.Services.AddRazorPages(options =>
{
    options.Conventions.AuthorizeFolder("/Admin");
    options.Conventions.AuthorizeFolder("/Admin", "AdminOnly");
    options.Conventions.AuthorizeFolder("/Dashboard");
    options.Conventions.AuthorizeFolder("/Admin/DbStatus");
    // Allow anonymous for auth-related pages
    options.Conventions.AllowAnonymousToFolder("/hubs");
    
    
    options.Conventions.AllowAnonymousToFolder("/assets");
    
    options.Conventions.AllowAnonymousToFolder("/css");
    options.Conventions.AllowAnonymousToFolder("/js");
    options.Conventions.AllowAnonymousToPage("/Index");
    options.Conventions.AllowAnonymousToPage("/Contact");
    options.Conventions.AllowAnonymousToPage("/Chat");
    options.Conventions.AllowAnonymousToPage("/Cv");
    options.Conventions.AllowAnonymousToPage("/Cv");
    
    options.Conventions.AllowAnonymousToPage("/Admin/Login");
    options.Conventions.AllowAnonymousToPage("/Admin/Register");
    options.Conventions.AllowAnonymousToPage("/Admin/ConfirmEmail");
    options.Conventions.AllowAnonymousToPage("/Admin/ForgotPassword");
    options.Conventions.AllowAnonymousToPage("/Admin/ResetPassword");
    options.Conventions.AllowAnonymousToPage("/Projects");
    options.Conventions.AllowAnonymousToPage("/Blog");
});

// WebOptimizer (SCSS, bundling, minification)
builder.Services.AddWebOptimizer(pipeline =>
{
    // Compile SCSS to CSS bundle
    pipeline.AddScssBundle("/css/site.bundle.css",
        "scss/site.scss",
        "css/animate.css",
        "assets/css/site.min.css"
    );

    pipeline.MinifyCssFiles();

    // Bundle and minify site JS
    pipeline.AddJavaScriptBundle("/js/site.bundle.js",
        "lib/jquery/dist/jquery.min.js",
        "lib/jquery-validation/dist/jquery.validate.min.js",
        "lib/jquery-validation-unobtrusive/jquery.validate.unobtrusive.min.js",
        "lib/bootstrap/dist/js/bootstrap.bundle.min.js",
        "js/signalR.js",
        "js/voicechat.js",
        "js/notifications.js",
        "js/site.js"
    );

    pipeline.MinifyJsFiles();
});

// EF Core - SQLite (stable data folder)
var startupLogger = builder.Services.BuildServiceProvider().GetRequiredService<ILogger<Program>>();
var dbFilePath = DatabaseInitializer.GetDatabasePath(builder.Environment.ContentRootPath, startupLogger);
builder.Services.AddDbContext<ApplicationDbContext>(options => options.UseSqlite(builder.Configuration.GetConnectionString("Default")));

// Identity (Users + Roles)
builder.Services.AddIdentity<IdentityUser, IdentityRole>(options =>
{
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireUppercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequiredLength = 6;
    options.SignIn.RequireConfirmedEmail = false;
})
.AddEntityFrameworkStores<ApplicationDbContext>()
.AddDefaultTokenProviders();

// Configure the application cookie (instead of re-adding schemes already registered by AddIdentity)
builder.Services.ConfigureApplicationCookie(o =>
{
    o.LoginPath = "/Admin/Login";
    o.AccessDeniedPath = "/Admin/Login";
    o.ExpireTimeSpan = TimeSpan.FromHours(8);
    o.Cookie.SameSite = SameSiteMode.Strict;
    o.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

builder.Services.Configure<JsonOptions>(options =>
{
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});



// Detect Redis configuration and wire distributed services if present
var redisConn = builder.Configuration["Redis:Connection"] ?? builder.Configuration["REDIS__CONNECTION"];
if (!string.IsNullOrWhiteSpace(redisConn))
{
    // Distributed cache (StackExchange Redis) - FIXED: use AddStackExchangeRedisCache
    builder.Services.AddStackExchangeRedisCache(opts =>
    {
        opts.Configuration = redisConn;
        opts.InstanceName = "RadosAi:";
    });
    builder.Services.AddMemoryCache(); // <- ensure IMemoryCache is available for services that expect it

    // Persist DataProtection keys to Redis and register multiplexer for other uses
    var mux = ConnectionMultiplexer.Connect(redisConn);
    builder.Services.AddSingleton<IConnectionMultiplexer>(mux);
    builder.Services.AddDataProtection()
        .PersistKeysToStackExchangeRedis(mux, "DataProtection-Keys")
        .SetApplicationName("RadosAiPortfolio");

    // Use distributed cache-backed session behavior (session will use IDistributedCache)
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
        options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    });
}
else
{
    // Fallback to in-memory distributed cache for single-instance/local
    builder.Services.AddMemoryCache();
    builder.Services.AddDistributedMemoryCache();
    builder.Services.AddSignalR();
    builder.Services.AddSession(options =>
    {
        options.IdleTimeout = TimeSpan.FromMinutes(30);
        options.Cookie.HttpOnly = true;
        options.Cookie.IsEssential = true;
    });
}

// Register HttpClient factory and named clients for local models
builder.Services.AddHttpClient();

// Register typed HttpClient for Gpt4All and Olama
builder.Services.AddHttpClient<Gpt4AllClient>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var url = cfg["Gpt4All:Url"];
    if (!string.IsNullOrWhiteSpace(url))
    {
        try { client.BaseAddress = new Uri(url); } catch { }
    }
    if (int.TryParse(cfg["Gpt4All:TimeoutSeconds"], out var s)) client.Timeout = TimeSpan.FromSeconds(s);
});

builder.Services.AddHttpClient<OlamaClient>((sp, client) =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var url = cfg["Olama:Url"];
    if (!string.IsNullOrWhiteSpace(url))
    {
        try { client.BaseAddress = new Uri(url); } catch { }
    }
    if (int.TryParse(cfg["Olama:TimeoutSeconds"], out var s)) client.Timeout = TimeSpan.FromSeconds(s);
});

// Use local model client as the AI backend if available
builder.Services.AddScoped<ILocalModelClient>(sp =>
{
    var cfg = sp.GetRequiredService<IConfiguration>();
    var prefer = cfg["LocalModel:Preferred"] ?? string.Empty;
    if (prefer.Equals("gpt4all", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<Gpt4AllClient>();
    if (prefer.Equals("olama", StringComparison.OrdinalIgnoreCase))
        return sp.GetRequiredService<OlamaClient>();

    // default: use GPT4All if present
    return sp.GetRequiredService<Gpt4AllClient>();
});

// Register embedding client (local/dev fallback)
builder.Services.AddScoped<IEmbeddingClient, LocalEmbeddingClient>();

builder.Services.AddScoped<IRagService, EmbeddingRagService>();
builder.Services.AddScoped<AIService>();

// Reindex background queue: register concrete service and expose as IReindexManager
builder.Services.AddSingleton<ReindexQueueService>();
builder.Services.AddSingleton<IReindexManager>(sp => sp.GetRequiredService<ReindexQueueService>());
builder.Services.AddHostedService(sp => sp.GetRequiredService<ReindexQueueService>());

builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build())
    .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

// Email services
builder.Services.AddSingleton<IMailSender, MailSenderService>();
builder.Services.AddSingleton<MailQueueService>();
builder.Services.AddHostedService<MailQueueService>(provider => provider.GetRequiredService<MailQueueService>());

// Register ChatMetricsService for lightweight server-side metrics
builder.Services.AddSingleton<ChatMetricsService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

// Forwarded headers for reverse proxy (Nginx)
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
// HTTPS redirect
//app.UseHttpsRedirection();
// WebOptimizer must come BEFORE StaticFiles
app.UseWebOptimizer();
// Serve static files (CSS, JS, fonts, etc.)
app.UseStaticFiles();

// Routing
app.UseRouting();
// Session before auth
app.UseSession();
// Auth after static files
app.UseAuthentication();
app.UseAuthorization();
// Map endpoints
app.MapStaticAssets();
app.MapRazorPages().WithStaticAssets();

// SignalR hub
app.MapHub<NotificationsHub>("/hubs/notifications");

// Keep existing API redirect for case-insensitive route
app.MapGet("/api/chat", (HttpContext ctx) => Results.Redirect("/Api/Chat")).ExcludeFromDescription();

// Endpoint: return indexed RAG snippets for UI
app.MapGet("/api/ai/snippets", async (IRagService rag) =>
{
    try
    {
        var list = await rag.GetIndexedSnippetsAsync();
        return Results.Ok(list);
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, error = ex.Message }, statusCode: 500);
    }
}).ExcludeFromDescription();

// Diagnostic endpoint: show configured URLs/environment settings related to hosting
app.MapGet("/api/diagnostics/urls", (IConfiguration cfg) =>
{
    var envUrls = Environment.GetEnvironmentVariable("ASPNETCORE_URLS") ?? Environment.GetEnvironmentVariable("ASPNETCORE_URLS");
    var urlsFromConfig = cfg["Urls"] ?? cfg["Kestrel:Endpoints:Http:Url"] ?? cfg["Kestrel:Endpoints:Https:Url"];
    var result = new
    {
        configUrls = urlsFromConfig,
        envAspNetcoreUrls = envUrls,
        appsettingsAspNetcoreUrls = cfg["ASPNETCORE_URLS"]
    };
    return Results.Ok(result);
}).ExcludeFromDescription();

// Diagnostic endpoint: quick AI health/test (returns sample answer or error)
app.MapGet("/api/ai/test", async (AIService ai) =>
{
    try
    {
        var sample = await ai.AskAsync("Hello, who are you?");
        return Results.Ok(new { ok = true, sample });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, error = ex.Message }, statusCode: 500);
    }
}).ExcludeFromDescription();

// Admin-only diagnostics: index count, AI configured sample, last status
app.MapGet("/api/ai/diagnostics", async (AIService ai, IRagService rag) =>
{
    try
    {
        var indexCount = await rag.GetIndexCountAsync();
        string aiSample;
        try
        {
            aiSample = await ai.AskAsync("Give a one-line summary of Radoš' portfolio.");
        }
        catch (Exception ex)
        {
            aiSample = "AI call failed: " + ex.Message;
        }

        return Results.Ok(new { ok = true, indexCount, aiSample });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, error = ex.Message }, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly").ExcludeFromDescription();

// Admin-only: force rebuild of RAG index (may call embeddings and consume quota)
app.MapPost("/api/ai/reindex", async (IRagService rag, ILogger<Program> logger) =>
{
    try
    {
        logger.LogInformation("Admin triggered RAG reindex.");
        await rag.RebuildIndexAsync();
        var count = await rag.GetIndexCountAsync();
        return Results.Ok(new { ok = true, message = "Reindex complete", count });
    }
    catch (HttpRequestException qx) when (qx.Data.Contains("QuotaExceeded") && qx.Data["QuotaExceeded"] is bool b && b)
    {
        TimeSpan? retryAfter = null;
        if (qx.Data.Contains("RetryAfter") && qx.Data["RetryAfter"] is TimeSpan ra) retryAfter = ra;
        return Results.Json(new { ok = false, error = "Quota exceeded", retryAfter = retryAfter?.TotalSeconds }, statusCode: 429);
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, error = ex.Message }, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly").ExcludeFromDescription();

// Admin-only: enqueue background reindex job
app.MapPost("/api/ai/reindex/background", async (IReindexManager mgr, ILogger<Program> logger) =>
{
    try
    {
        var jobId = await mgr.EnqueueReindexAsync();
        return Results.Ok(new { ok = true, jobId });
    }
    catch (Exception ex)
    {
        return Results.Json(new { ok = false, error = ex.Message }, statusCode: 500);
    }
}).RequireAuthorization("AdminOnly").ExcludeFromDescription();

// Admin-only: get reindex status
app.MapGet("/api/ai/reindex/status", (IReindexManager mgr) =>
{
    var status = mgr.GetStatus();
    return Results.Ok(status);
}).RequireAuthorization("AdminOnly").ExcludeFromDescription();

// Clean database setup: single call to DirectoryHelper
await DatabaseInitializer.SetupDatabaseAsync(app.Services, app.Environment.ContentRootPath);

// API endpoint to rate a project
app.MapPost("/api/blograte", async (HttpContext ctx, ApplicationDbContext db) =>
{
    var json = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
    var data = System.Text.Json.JsonSerializer.Deserialize<BlogRateRequest>(json);

    if (data is null || data.blogId <= 0 || data.stars < 1 || data.stars > 5)
        return Results.BadRequest();

    // Save or update the rating (Comment) for the blog post
    var comment = new Comment
    {
        BlogId = data.blogId,
        Stars = data.stars,
        CommentText = "" // Optional: you can extend to allow comments
    };
    db.Comments.Add(comment);
    await db.SaveChangesAsync();

    var avg = await db.Comments.Where(c => c.BlogId == data.blogId).AverageAsync(c => c.Stars);
    return Results.Ok(new { ok = true, average = avg });
});

// C# - expects JSON; minimal API will bind from body
app.MapPost("/api/rate", async (RateRequest request, ApplicationDbContext db) =>
{
    if (request.projectId <= 0 || request.stars < 1 || request.stars > 5) return Results.BadRequest();
    db.Comments.Add(new Comment { ProjectId = request.projectId, Stars = request.stars, CommentText = "" });
    await db.SaveChangesAsync();
    var avg = await db.Comments.Where(c => c.ProjectId == request.projectId).AverageAsync(c => c.Stars);
    return Results.Ok(new { ok = true, average = avg });
});

// Feedback endpoint (existing)
app.MapPost("/api/feedback", async (HttpContext ctx, ApplicationDbContext db) =>
{
    try
    {
        var json = await new StreamReader(ctx.Request.Body).ReadToEndAsync();
        var data = System.Text.Json.JsonSerializer.Deserialize<FeedbackRequest>(json);

        if (data is null || string.IsNullOrWhiteSpace(data.Question) || string.IsNullOrWhiteSpace(data.Answer) || data.Rating == 0)
            return Results.BadRequest();

        var saved = await db.SavedInteractions
            .Where(s => s.Question == data.Question && s.Answer == data.Answer)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();

        if (saved != null)
        {
            saved.UserRating = data.Rating;
            db.SavedInteractions.Update(saved);
        }
        else
        {
            db.SavedInteractions.Add(new SavedInteraction
            {
                Question = data.Question,
                Answer = data.Answer,
                UserRating = data.Rating,
                CreatedAt = DateTime.UtcNow
            });
        }

        await db.SaveChangesAsync();
        return Results.Ok();
    }
    catch
    {
        return Results.StatusCode(500);
    }
});

// Expose POST /api/chat (JSON) so client can POST to /api/chat reliably
app.MapPost("/api/chat", async (HttpContext ctx, AIService ai, ApplicationDbContext db, IHubContext<NotificationsHub> hub, ILogger<Program> logger, ChatMetricsService metrics) =>
{
    var sw = System.Diagnostics.Stopwatch.StartNew();
    metrics.RecordRequest();
    try
    {
        using var reader = new StreamReader(ctx.Request.Body);
        var body = await reader.ReadToEndAsync();
        var req = System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(body);
        if (req == null || !req.TryGetValue("message", out var message) || string.IsNullOrWhiteSpace(message))
            return Results.BadRequest(new { error = "Message is required." });

        const string SessionKey = "chatmem";
        const string SessionIdKey = "chatsessionid";

        // Ensure session id
        var sessionId = ctx.Session.GetString(SessionIdKey);
        if (string.IsNullOrEmpty(sessionId))
        {
            sessionId = Guid.NewGuid().ToString();
            ctx.Session.SetString(SessionIdKey, sessionId);
        }

        // Load recent chat history with fallback if Timestamp column is missing
        List<Chat> history;
        try
        {
            history = await db.Chats
                .Where(c => c.ChatSessionId == sessionId)
                .OrderBy(c => c.Timestamp)
                .Take(50)
                .Select(c => new Chat { Role = c.Role, Content = c.Content, Response = c.Response, ChatSessionId = c.ChatSessionId, Timestamp = c.Timestamp })
                .ToListAsync();
        }
        catch (SqliteException ex) when (ex.Message?.Contains("no such column", StringComparison.OrdinalIgnoreCase) == true)
        {
            history = await db.Chats
                .Where(c => c.ChatSessionId == sessionId)
                .OrderBy(c => c.Id)
                .Take(50)
                .Select(c => new Chat { Role = c.Role, Content = c.Content, Response = c.Response, ChatSessionId = c.ChatSessionId })
                .ToListAsync();
        }

        // Persist user turn
        var userTurn = new Chat { Role = "user", Content = message, ChatSessionId = sessionId, Timestamp = DateTime.UtcNow };
        db.Chats.Add(userTurn);
        await db.SaveChangesAsync();

        var mem = history.Select(h => new Chat { Role = h.Role, Content = h.Content }).ToList();
        mem.Add(new Chat { Role = "user", Content = message });

        string response;
        try
        {
            response = await ai.AskAsync(message, mem);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "AIService failed to produce a response");
            metrics.RecordError(sw.Elapsed);
            return Results.Json(new { error = "AI service error", detail = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(response))
            response = "I'm not sure how to answer that.";

        var assistantTurn = new Chat { Role = "assistant", Content = response, ChatSessionId = sessionId, Timestamp = DateTime.UtcNow };
        db.Chats.Add(assistantTurn);
        await db.SaveChangesAsync();

        // Extract 'Sources:' line if present
        var sources = new List<string>();
        try
        {
            var m = System.Text.RegularExpressions.Regex.Match(response ?? string.Empty, @"(?m)^Sources?:\s*(.+)$", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if (m.Success)
            {
                var raw = m.Groups[1].Value ?? string.Empty;
                sources = raw.Split(',').Select(s => s.Trim()).Where(s => s.Length > 0).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
            }
        }
        catch { }

        try
        {
            var saved = new SavedInteraction
            {
                Question = message,
                Answer = response,
                Source = sources.Count > 0 ? string.Join(", ", sources) : null,
                CreatedAt = DateTime.UtcNow
            };
            db.SavedInteractions.Add(saved);
            await db.SaveChangesAsync();

            await hub.Clients.All.SendAsync("ReceiveNotification", new { title = "New chat question", message = message, timestamp = DateTime.UtcNow });
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to save interaction to SavedInteractions");
        }

        // Retrieve recent chat turns with same fallback
        List<Chat> recent;
        try
        {
            recent = await db.Chats.Where(c => c.ChatSessionId == sessionId).OrderBy(c => c.Timestamp).Take(50).ToListAsync();
        }
        catch (SqliteException ex) when (ex.Message?.Contains("no such column", StringComparison.OrdinalIgnoreCase) == true)
        {
            recent = await db.Chats.Where(c => c.ChatSessionId == sessionId).OrderBy(c => c.Id).Take(50)
                .Select(c => new Chat { Role = c.Role, Content = c.Content, Response = c.Response, ChatSessionId = c.ChatSessionId })
                .ToListAsync();
        }
        ctx.Session.SetString(SessionKey, System.Text.Json.JsonSerializer.Serialize(recent));

        metrics.RecordSuccess(sw.Elapsed);

        return Results.Json(new { reply = response, response, sources });
    }
    catch (Exception ex)
    {
        var errorLogger = ctx.RequestServices.GetRequiredService<ILogger<Program>>();
        errorLogger.LogError(ex, "Error processing chat request (minimal API)");
        var devDetail = System.Diagnostics.Debugger.IsAttached ? ex.ToString() : null;
        metrics.RecordError(sw.Elapsed);
        return Results.Json(new { error = "An error occurred while processing your request.", detail = devDetail });
    }
}).ExcludeFromDescription();

app.MapGet("/api/ai/chatmetrics", (ChatMetricsService metrics) =>
{
    var snap = metrics.GetSnapshot();
    return Results.Ok(snap);
}).RequireAuthorization("AdminOnly").ExcludeFromDescription();

app.Run();

public record RateRequest(int projectId, int stars);
public record BlogRateRequest(int blogId, int stars);
public record FeedbackRequest(string Question, string Answer, int Rating);