using MarketplacesGoodsTracker.Data;
using MarketplacesGoodsTracker.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Telegram.Bot;

var builder = Host.CreateApplicationBuilder(args);

// Configuration
builder.Configuration.AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

// Bot Client
var botToken = builder.Configuration["BotToken"];
if (string.IsNullOrEmpty(botToken) || botToken == "YOUR_TELEGRAM_BOT_TOKEN")
{
    Console.WriteLine("Please provide a valid BotToken in appsettings.json");
    return;
}

builder.Services.AddSingleton<ITelegramBotClient>(new TelegramBotClient(botToken));

// Services
builder.Services.AddSingleton<DatabaseService>();
builder.Services.AddSingleton<WbParserService>();
builder.Services.AddHostedService<TelegramBotService>();
builder.Services.AddHostedService<PriceTrackerService>();

using IHost host = builder.Build();

await host.RunAsync();
