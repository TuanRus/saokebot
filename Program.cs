using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Builder;
using SaoKeBot.Models;
using SaoKeBot.Services;
using SaoKeBot.Services.Features;
using SaoKeBot.Services.Repositories;

var builder = WebApplication.CreateBuilder(args);

// --- 1. Register Services ---

builder.Services.Configure<BotConfiguration>(builder.Configuration.GetSection("BotConfiguration"));

builder.Services.AddSingleton<MessageParser>();

// Pick the storage layer via Storage:Provider ("Sqlite" by default, or "DynamoDB").
// Local development uses SQLite for testing.
var storageProvider = builder.Configuration["Storage:Provider"] ?? "Sqlite";
if (storageProvider.Equals("DynamoDB", StringComparison.OrdinalIgnoreCase))
{
    var tableName = builder.Configuration["Storage:TableName"] ?? "SaoKeTransactions";
    builder.Services.AddAWSService<Amazon.DynamoDBv2.IAmazonDynamoDB>();
    builder.Services.AddSingleton<ITransactionRepository>(sp =>
        new DynamoTransactionRepository(sp.GetRequiredService<Amazon.DynamoDBv2.IAmazonDynamoDB>(), tableName));
    Console.WriteLine($"[STORAGE] Using DynamoDB (table: {tableName}).");
}
else
{
    builder.Services.AddSingleton<ITransactionRepository, SqliteTransactionRepository>();
    Console.WriteLine("[STORAGE] Using SQLite.");
}

// Register the Core Services split out following the SRP model.
builder.Services.AddSingleton<ConfigService>();
builder.Services.AddSingleton<AuthService>();

builder.Services.AddSingleton<TelegramBotService>();

builder.Services.AddRazorPages();

builder.Services.AddSingleton<IFeatureHandler, ExpenseFeature>();
builder.Services.AddSingleton<IFeatureHandler, IncomeFeature>();
builder.Services.AddSingleton<IFeatureHandler, DebtFeature>();
builder.Services.AddSingleton<IFeatureHandler, LoanFeature>();
builder.Services.AddSingleton<IFeatureHandler, GetbackFeature>();
builder.Services.AddSingleton<IFeatureHandler, PaybackFeature>();

var app = builder.Build();

// --- 2. Init storage + start the Bot ---

// Ensure the table/schema exists before handling transactions.
await app.Services.GetRequiredService<ITransactionRepository>().InitAsync();

var botService = app.Services.GetRequiredService<TelegramBotService>();
botService.Start();

Console.WriteLine("[SYSTEM] Telegram Bot Engine and Services injected successfully.");
Console.WriteLine("Bot is running!!...");

// --- 3. Configure Web Server ---
app.UseStaticFiles();
app.MapRazorPages();
app.Run();
