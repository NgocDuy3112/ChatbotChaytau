using chatbotchaytau.Components;
using chatbotchaytau.Data;
using chatbotchaytau.Extensions;
using chatbotchaytau.Services;
using chatbotchaytau.Services.Conversations;
using chatbotchaytau.Services.Documents;
using chatbotchaytau.Services.Ingestion;
using chatbotchaytau.Services.Agents;
using chatbotchaytau.Services.RateLimit;
using chatbotchaytau.Services.Usage;
using chatbotchaytau.Services.WebSearch;
using DotNetEnv;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using StackExchange.Redis;
using System.ClientModel;
using Microsoft.AspNetCore.DataProtection;

Env.Load();

// QuestPDF refuses to generate until a license is set; Community is free for
// this project's scale. Must happen before the first document is written.
QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
// Requested fonts (e.g. Courier New for code blocks) may be absent on slim
// hosts/containers — render with the closest available font instead of
// aborting document generation.
QuestPDF.Settings.ThrowOnMissingFontFamilies = false;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddAppIdentity(builder.Configuration, builder.Environment);
builder.Services.AddScoped<IConversationStore, ConversationStore>();

// --- Redis-backed infrastructure -----------------------------------------
// Redis:Enabled is true in docker-compose and false locally, so `dotnet run`
// keeps working without a Redis server (memory fallbacks below).
var redisEnabled = builder.Configuration.GetValue<bool>("Redis:Enabled");
if (redisEnabled)
{
    var redisConnection = builder.Configuration["Redis:ConnectionString"]
        ?? throw new InvalidOperationException("Missing configuration: Redis__ConnectionString");
    var redis = ConnectionMultiplexer.Connect(redisConnection);
    builder.Services.AddSingleton<IConnectionMultiplexer>(redis);

    // Cookie encryption keys live in Redis — restarts/redeploys no longer
    // log everyone out (previously keys died with the container filesystem).
    builder.Services.AddDataProtection()
        .SetApplicationName("chatbotchaytau")
        .PersistKeysToStackExchangeRedis(redis, "chatbotchaytau:data-protection");

    builder.Services.AddStackExchangeRedisCache(o =>
    {
        o.Configuration = redisConnection;
        o.InstanceName = "chatbotchaytau:";
    });
}
else
{
    builder.Services.AddDataProtection().SetApplicationName("chatbotchaytau");
    builder.Services.AddDistributedMemoryCache();
}

builder.Services.AddSingleton<IChatRateLimiter>(sp => new ChatRateLimiter(
    redisEnabled ? sp.GetRequiredService<IConnectionMultiplexer>() : null,
    sp.GetRequiredService<IConfiguration>(),
    sp.GetRequiredService<ILogger<ChatRateLimiter>>()));

// --- usage / cost tracking (admin portal) --------------------------------
builder.Services.AddSingleton(new PricingTable(builder.Configuration));
builder.Services.AddScoped<IUsageTracker, UsageTracker>();
builder.Services.AddScoped<IUsageReport, UsageReport>();

// chat stack
var or = builder.Configuration.GetSection("OpenRouter");
var options = new OpenAIClientOptions { 
    Endpoint = new Uri(or["BaseUrl"] ?? "https://openrouter.ai/api/v1") 
};
var openAIClient = new OpenAIClient(new ApiKeyCredential(or["ApiKey"]!), options);
builder.Services.AddSingleton(openAIClient);

#pragma warning disable OPENAI001
var chatClient = openAIClient.GetResponsesClient().AsIChatClient(or["LightModel"]!);
#pragma warning restore OPENAI001
var embeddings = openAIClient.GetEmbeddingClient(or["EmbeddingModel"]!).AsIEmbeddingGenerator();

builder.Services.AddHttpClient<SystemOneRouter>();
builder.Services.AddHttpClient<WebSearchTool>();
builder.Services.AddScoped<ParallelResearchTool>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddScoped<DocumentIngestor>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddChatClient(chatClient).UseLogging();
builder.Services.AddEmbeddingGenerator(embeddings);

// Behind a proxy/container the real scheme arrives as X-Forwarded-Proto —
// without this, Google OAuth callbacks and Secure cookies misbehave in prod.
builder.Services.Configure<ForwardedHeadersOptions>(o =>
{
    o.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
    // One known proxy sits in front (compose/host gateway) on a private
    // network, so the dynamic client-address lists don't apply here.
    o.KnownIPNetworks.Clear();
    o.KnownProxies.Clear();
});

var app = builder.Build();

// Must run first so everything downstream sees the corrected scheme/host.
app.UseForwardedHeaders();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
    app.UseHttpsRedirection();
}

app.UseAuthentication();
app.UseAuthorization();
app.UseAntiforgery();
app.UseStaticFiles();

app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.MapAuthEndpoints(app.Environment);
app.MapDocumentEndpoints();

if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.Run();