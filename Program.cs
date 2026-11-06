using System.ClientModel;
using Microsoft.Extensions.AI;
using OpenAI;
using chatbotchaytau.Components;
using chatbotchaytau.Services;
using chatbotchaytau.Services.Ingestion;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorComponents().AddInteractiveServerComponents();

var or = builder.Configuration.GetSection("OpenRouter");
var apiKey = or["ApiKey"] ?? throw new InvalidOperationException("Missing configuration: OpenRouter:ApiKey");
var baseUrl = or["BaseUrl"] ?? "https://openrouter.ai/api/v1";
var chatModel = or["LightModel"] ?? "openai/gpt-4o-mini";
var embeddingModel = or["EmbeddingModel"] ?? "openai/text-embedding-3-small";

var options = new OpenAIClientOptions { Endpoint = new Uri(baseUrl) };
var openAIClient = new OpenAIClient(new ApiKeyCredential(apiKey), options);

#pragma warning disable OPENAI001
var chatClient = openAIClient.GetResponsesClient().AsIChatClient(chatModel);
#pragma warning restore OPENAI001

var embeddingGenerator = openAIClient.GetEmbeddingClient(embeddingModel).AsIEmbeddingGenerator();

var vectorStorePath = Path.Combine(AppContext.BaseDirectory, "vector-store.db");
var vectorStoreConnectionString = $"Data Source={vectorStorePath}";
builder.Services.AddSqliteVectorStore(_ => vectorStoreConnectionString);
builder.Services.AddSqliteCollection<Guid, IngestedChunk>(IngestedChunk.CollectionName, vectorStoreConnectionString);

builder.Services.AddHttpClient<SystemOneRouter>();
builder.Services.AddSingleton<SystemOneRouter>();
builder.Services.AddSingleton<DataIngestor>();
builder.Services.AddSingleton<SemanticSearch>();
builder.Services.AddKeyedSingleton("ingestion_directory", new DirectoryInfo(Path.Combine(builder.Environment.WebRootPath, "Data")));
builder.Services.AddChatClient(chatClient).UseFunctionInvocation().UseLogging();
builder.Services.AddEmbeddingGenerator(embeddingGenerator);

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseAntiforgery();
app.UseStaticFiles();
app.MapRazorComponents<App>().AddInteractiveServerRenderMode();
app.Run();
