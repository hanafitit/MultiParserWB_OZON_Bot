using MarketplacesGoodsTracker.Data;
using MarketplacesGoodsTracker.Models;
using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace MarketplacesGoodsTracker.Services;

public class PriceTrackerService : BackgroundService
{
    private readonly DatabaseService _dbService;
    private readonly WbParserService _parserService;
    private readonly ITelegramBotClient _botClient;
    private readonly ILogger<PriceTrackerService> _logger;
    private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

    public PriceTrackerService(
        DatabaseService dbService,
        WbParserService parserService,
        ITelegramBotClient botClient,
        ILogger<PriceTrackerService> logger)
    {
        _dbService = dbService;
        _parserService = parserService;
        _botClient = botClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Price Tracker Service is starting.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await TrackPricesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error occurred while tracking prices.");
            }

            await Task.Delay(_checkInterval, stoppingToken);
        }
    }

    private async Task TrackPricesAsync(CancellationToken stoppingToken)
    {
        var products = await _dbService.GetAllTrackedProductsAsync();
        _logger.LogInformation("Checking prices for {Count} products.", products.Count());

        foreach (var product in products)
        {
            if (stoppingToken.IsCancellationRequested) break;

            var currentData = await _parserService.GetProductAsync(product.SKU);
            if (currentData == null) continue;

            if (currentData.Price < product.LastKnownPrice)
            {
                var text = $"📉 *Цена снизилась!*\n\n" +
                           $"📦 [{product.Name}]({product.ProductUrl})\n" +
                           $"💰 Старая цена: {product.LastKnownPrice} ₽\n" +
                           $"🔥 Новая цена: {currentData.Price} ₽\n\n" +
                           $"Скорее покупай, пока не разобрали!";

                await _botClient.SendMessage(
                    chatId: product.ChatId,
                    text: text,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: stoppingToken);

                await _dbService.UpdateProductPriceAsync(product.Id, currentData.Price);
            }
            else if (currentData.Price != product.LastKnownPrice)
            {
                // Если цена выросла, просто обновляем последнее известное значение без уведомления
                await _dbService.UpdateProductPriceAsync(product.Id, currentData.Price);
            }

            // Небольшая задержка между запросами, чтобы не спамить WB
            await Task.Delay(2000, stoppingToken);
        }
    }
}
