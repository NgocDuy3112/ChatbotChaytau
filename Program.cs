using chatbotchaytau.Components;
using chatbotchaytau.Data;
using chatbotchaytau.Extensions;
using chatbotchaytau.Services;
using chatbotchaytau.Services.Conversations;
using chatbotchaytau.Services.Documents;
using chatbotchaytau.Services.Ingestion;
using DotNetEnv;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.AI;
using OpenAI;
using System.ClientModel;

Env.Load();

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

builder.Services.AddAppIdentity(builder.Configuration, builder.Environment);
builder.Services.AddScoped<IConversationStore, ConversationStore>();

// chat stack
var or = builder.Configuration.GetSection("OpenRouter");
var options = new OpenAIClientOptions { 
    Endpoint = new Uri(or["BaseUrl"] ?? "https://openrouter.ai/api/v1") 
};
var openAIClient = new OpenAIClient(new ApiKeyCredential(or["ApiKey"]!), options);

#pragma warning disable OPENAI001
var chatClient = openAIClient.GetResponsesClient().AsIChatClient(or["LightModel"]!);
#pragma warning restore OPENAI001
var embeddings = openAIClient.GetEmbeddingClient(or["EmbeddingModel"]!).AsIEmbeddingGenerator();

builder.Services.AddHttpClient<SystemOneRouter>();
builder.Services.AddSingleton<SystemOneRouter>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddScoped<DocumentIngestor>();
builder.Services.AddScoped<DocumentService>();
builder.Services.AddChatClient(chatClient).UseLogging();
builder.Services.AddEmbeddingGenerator(embeddings);

var app = builder.Build();

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