using System.Text.RegularExpressions;
using MarketplacesGoodsTracker.Data;
using MarketplacesGoodsTracker.Models;
using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Hosting;

namespace MarketplacesGoodsTracker.Services;

public class TelegramBotService : BackgroundService
{
    private readonly ITelegramBotClient _botClient;
    private readonly DatabaseService _dbService;
    private readonly WbParserService _parserService;
    private readonly ILogger<TelegramBotService> _logger;

    public TelegramBotService(
        ITelegramBotClient botClient,
        DatabaseService dbService,
        WbParserService parserService,
        ILogger<TelegramBotService> logger)
    {
        _botClient = botClient;
        _dbService = dbService;
        _parserService = parserService;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>()
        };

        _botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            errorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: stoppingToken
        );

        var me = await _botClient.GetMe(stoppingToken);
        _logger.LogInformation("Start listening for @{BotName}", me.Username);
    }

    private async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Message is { } message && message.Text is { } messageText)
        {
            var chatId = message.Chat.Id;

            if (messageText == "/start")
            {
                await _dbService.AddUserAsync(new Models.User
                {
                    ChatId = chatId,
                    IsActive = true,
                    RegistrationDate = DateTime.UtcNow
                });

                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Привет! Пришли мне ссылку на товар с Wildberries, и я помогу тебе отслеживать его.",
                    cancellationToken: cancellationToken);
                return;
            }

            var sku = ExtractSku(messageText);
            if (!string.IsNullOrEmpty(sku))
            {
                var inlineKeyboard = new InlineKeyboardMarkup(new[]
                {
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("📊 Получить данные", $"get_{sku}"),
                        InlineKeyboardButton.WithCallbackData("🔔 Отслеживать цену", $"track_{sku}"),
                    },
                    new []
                    {
                        InlineKeyboardButton.WithCallbackData("❌ Отмена", "cancel")
                    }
                });

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"Я нашел артикул {sku}. Что нужно сделать?",
                    replyMarkup: inlineKeyboard,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(
                    chatId: chatId,
                    text: "Пожалуйста, пришли корректную ссылку на товар Wildberries.",
                    cancellationToken: cancellationToken);
            }
        }
        else if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
        {
            await HandleCallbackQuery(botClient, update.CallbackQuery, cancellationToken);
        }
    }

    private async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery, CancellationToken cancellationToken)
    {
        var chatId = callbackQuery.Message!.Chat.Id;
        var data = callbackQuery.Data;

        if (data == "cancel")
        {
            await botClient.EditMessageText(
                chatId: chatId,
                messageId: callbackQuery.Message.MessageId,
                text: "Действие отменено.",
                cancellationToken: cancellationToken);
            return;
        }

        if (data!.StartsWith("get_"))
        {
            var sku = data.Replace("get_", "");
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "Собираю данные...", cancellationToken: cancellationToken);

            var product = await _parserService.GetProductAsync(sku);
            if (product != null)
            {
                var text = $"📦 *{product.Name}*\n" +
                           $"💰 Цена: {product.Price} ₽\n" +
                           $"🏷 Скидка: {product.SalePrice - product.Price} ₽\n" +
                           $"⭐️ Рейтинг: {product.Rating}\n" +
                           $"💬 Отзывы: {product.Feedbacks}\n" +
                           $"🏬 Остатки: {product.Stocks}";

                await botClient.SendMessage(
                    chatId: chatId,
                    text: text,
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(chatId: chatId, text: "Не удалось получить данные о товаре.", cancellationToken: cancellationToken);
            }
        }
        else if (data.StartsWith("track_"))
        {
            var sku = data.Replace("track_", "");
            await botClient.AnswerCallbackQuery(callbackQuery.Id, "Подписываюсь на уведомления...", cancellationToken: cancellationToken);

            var product = await _parserService.GetProductAsync(sku);
            if (product != null)
            {
                await _dbService.AddTrackedProductAsync(new TrackedProduct
                {
                    ChatId = chatId,
                    SKU = sku,
                    Name = product.Name,
                    ProductUrl = $"https://www.wildberries.ru/catalog/{sku}/detail.aspx",
                    TargetPrice = product.Price,
                    LastKnownPrice = product.Price
                });

                await botClient.SendMessage(
                    chatId: chatId,
                    text: $"✅ Вы подписались на товар *{product.Name}*. Я сообщу, если цена упадет ниже {product.Price} ₽.",
                    parseMode: ParseMode.Markdown,
                    cancellationToken: cancellationToken);
            }
            else
            {
                await botClient.SendMessage(chatId: chatId, text: "Не удалось добавить товар для отслеживания.", cancellationToken: cancellationToken);
            }
        }
    }

    private Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, HandleErrorSource source, CancellationToken cancellationToken)
    {
        _logger.LogError(exception, "Telegram API Error from {Source}", source);
        return Task.CompletedTask;
    }

    private string? ExtractSku(string text)
    {
        var match = Regex.Match(text, @"(?:wildberries\.ru/catalog/|wb\.ru/catalog/|catalog/)(\d+)");
        if (match.Success) return match.Groups[1].Value;

        if (Regex.IsMatch(text, @"^\d+$")) return text;

        return null;
    }
}
