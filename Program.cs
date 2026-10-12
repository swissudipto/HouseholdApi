using HouseholdApi.Services;
using Microsoft.SemanticKernel;

var builder = WebApplication.CreateBuilder(args);

var storageProvider = builder.Configuration["Storage:Provider"] ?? "InMemory";
if (storageProvider.Equals("Sql", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<SqlMessageStore>();
    builder.Services.AddSingleton<IMessageStore>(services => services.GetRequiredService<SqlMessageStore>());
}
else if (storageProvider.Equals("InMemory", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<MessageStore>();
    builder.Services.AddSingleton<IMessageStore>(services => services.GetRequiredService<MessageStore>());
}
else
{
    throw new InvalidOperationException($"Unknown storage provider '{storageProvider}'. Use 'InMemory' or 'Sql'.");
}

var aiProvider = builder.Configuration["AI:Provider"] ?? "Placeholder";
if (aiProvider.Equals("Foundry", StringComparison.OrdinalIgnoreCase))
{
    var endpoint = builder.Configuration["AI:Foundry:Endpoint"];
    var model = builder.Configuration["AI:Foundry:Model"];
    var apiKey = builder.Configuration["AI:Foundry:ApiKey"];
    if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(model) || string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("AI:Foundry:Endpoint, Model, and ApiKey are required when AI:Provider is Foundry. Set the API key with .NET user-secrets or AI__Foundry__ApiKey.");

    builder.Services.AddKernel();
    builder.Services.AddOpenAIChatCompletion(model, new Uri(endpoint), apiKey);
    builder.Services.AddSingleton<IKnowledgeRetriever, LocalKnowledgeRetriever>();
    builder.Services.AddSingleton<IAiService, SemanticKernelAiService>();
}
else if (aiProvider.Equals("AzureOpenAI", StringComparison.OrdinalIgnoreCase))
{
    var endpoint = builder.Configuration["AI:AzureOpenAI:Endpoint"];
    var deployment = builder.Configuration["AI:AzureOpenAI:DeploymentName"];
    var apiKey = builder.Configuration["AI:AzureOpenAI:ApiKey"];
    if (string.IsNullOrWhiteSpace(endpoint) || string.IsNullOrWhiteSpace(deployment) || string.IsNullOrWhiteSpace(apiKey))
        throw new InvalidOperationException("AI:AzureOpenAI:Endpoint, DeploymentName, and ApiKey are required when AI:Provider is AzureOpenAI.");

    builder.Services.AddKernel();
    builder.Services.AddAzureOpenAIChatCompletion(deployment, endpoint, apiKey);
    builder.Services.AddSingleton<IKnowledgeRetriever, LocalKnowledgeRetriever>();
    builder.Services.AddSingleton<IAiService, SemanticKernelAiService>();
}
else if (aiProvider.Equals("Placeholder", StringComparison.OrdinalIgnoreCase))
{
    builder.Services.AddSingleton<IAiService, PlaceholderAiService>();
}
else
{
    throw new InvalidOperationException($"Unknown AI provider '{aiProvider}'. Use 'Placeholder', 'Foundry', or 'AzureOpenAI'.");
}
builder.Services.AddScoped<PasscodeFilter>();
builder.Services.AddControllers(options => options.Filters.AddService<PasscodeFilter>());

var origins = builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? [];
builder.Services.AddCors(options => options.AddDefaultPolicy(policy =>
    policy.WithOrigins(origins).WithHeaders("Content-Type", "X-Passcode").WithMethods("GET", "POST")));

var app = builder.Build();

if (storageProvider.Equals("Sql", StringComparison.OrdinalIgnoreCase))
{
    try { await app.Services.GetRequiredService<SqlMessageStore>().EnsureSchemaAsync(); }
    catch (Exception ex) { app.Logger.LogWarning(ex, "Could not ensure database schema on startup."); }
}

try
{
    var messageStore = app.Services.GetRequiredService<IMessageStore>();
    if ((await messageStore.ReadAsync(ConversationIds.LegacyDefault, 1, CancellationToken.None)).Count == 0)
        await messageStore.AddAsync(ConversationIds.LegacyDefault, "bot", "Hi Rani! I'm your personal assistant. What can I help you with today?", CancellationToken.None);
}
catch (Exception ex)
{
    app.Logger.LogWarning(ex, "Could not add the initial welcome message.");
}

app.UseCors();
app.MapControllers();

app.Run();
