using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.ReplyMarkups;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SaoKeBot.Models;
using SaoKeBot.Services.Features;

namespace SaoKeBot.Services
{
    public class TelegramBotService
    {
        private readonly TelegramBotClient _botClient;
        private readonly MessageParser _parser;
        private readonly AuthService _authService;
        private readonly ConfigService _config;
        private readonly SaoKeBot.Services.Repositories.ITransactionRepository _repo;
        private readonly IEnumerable<IFeatureHandler> _features;
        private readonly BotConfiguration _botConfig;

        // Web URL from config (env: BotConfiguration__WebUrl).
        private string WebUrl => _botConfig.WebUrl;

        public TelegramBotService(
            IOptions<BotConfiguration> config,
            MessageParser parser,
            AuthService authService,
            ConfigService configService,
            SaoKeBot.Services.Repositories.ITransactionRepository repo,
            IEnumerable<IFeatureHandler> features)
        {
            _botConfig = config.Value;
            _botClient = new TelegramBotClient(config.Value.BotToken);
            _parser = parser;
            _authService = authService;
            _config = configService;
            _repo = repo;
            _features = features;
        }

        public void Start()
        {
            var receiverOptions = new ReceiverOptions { AllowedUpdates = Array.Empty<UpdateType>() };
            _botClient.StartReceiving(HandleUpdateAsync, HandleErrorAsync, receiverOptions);
            Console.WriteLine("[SERVICE] Bot engine is active. MVC pattern applied.");
        }

        // Main event flow controller.
        private async Task HandleUpdateAsync(ITelegramBotClient bot, Update update, CancellationToken ct)
        {
            _config.EnsureConfigsLoaded();

            if (update.Type == UpdateType.CallbackQuery && update.CallbackQuery != null)
            {
                await HandleCallbackQueryAsync(bot, update.CallbackQuery);
                return;
            }

            if (update.Message is not { Text: { } messageText } message) return;

            long chatId = message.Chat.Id;
            long userId = message.From?.Id ?? 0;

            if (!_authService.IsAllowed(userId))
            {
                if (messageText.ToLower() == "/request" && message.From != null)
                    await SendApprovalRequestToAdmin(bot, message.From);
                else
                    await bot.SendTextMessageAsync(chatId, "Access denied. Type /request to ask for access.");
                return;
            }

            // Each user keeps their own separate data set (keyed by their own Telegram ID).
            long dataUserId = userId;

            if (await HandleSystemCommands(bot, chatId, dataUserId, messageText)) return;

            await ProcessTransaction(bot, chatId, dataUserId, messageText);
        }

        // Router for CLI commands.
        private async Task<bool> HandleSystemCommands(ITelegramBotClient bot, long chatId, long userId, string text)
        {
            string cmd = text.ToLower().Trim();

            if (cmd == "/start" || cmd == "/help" || cmd == "help")
            {
                await bot.SendTextMessageAsync(chatId,
                    "Syntax: [Item] [Amount] (e.g., Breakfast 30k)\nCommands: /dashboard, undo");
                return true;
            }

            if (cmd == "/dashboard" || cmd == "dashboard")
            {
                string token = DashboardToken.Generate(userId, _botConfig.DashboardSecret);
                var kb = new InlineKeyboardMarkup(
                    InlineKeyboardButton.WithUrl("View Dashboard", $"{WebUrl}?uid={userId}&token={token}"));
                await bot.SendTextMessageAsync(chatId, "Access your private statistics:", replyMarkup: kb);
                return true;
            }

            if (cmd == "delete" || cmd == "undo" || cmd == "cancel")
            {
                var lastTrans = await _repo.UndoLastAsync(userId);
                if (lastTrans != null)
                    await _botClient.SendTextMessageAsync(chatId, $"Deleted: {lastTrans.Note}");
                else
                    await _botClient.SendTextMessageAsync(chatId, "No records to delete.");
                return true;
            }

            return false;
        }

        // Maps parsed data to the corresponding Feature Logic.
        private async Task ProcessTransaction(ITelegramBotClient bot, long chatId, long userId, string text)
        {
            var result = _parser.Parse(text);
            if (result.Amount > 0)
            {
                var handler = _features.FirstOrDefault(f => f.CanHandle(result.Type));
                if (handler != null) await handler.HandleAsync(bot, chatId, userId, result);
            }
        }

        private async Task SendApprovalRequestToAdmin(ITelegramBotClient bot, User user)
        {
            var kb = new InlineKeyboardMarkup(new[] {
                new[] { InlineKeyboardButton.WithCallbackData("✅ Approve", $"approve_{user.Id}"),
                        InlineKeyboardButton.WithCallbackData("❌ Reject", $"reject_{user.Id}") }
            });

            foreach (var id in _config.GetAdmins())
            {
                try { await bot.SendTextMessageAsync(id, $"New Request: {user.FirstName} ({user.Id})", replyMarkup: kb); }
                catch { }
            }

            await bot.SendTextMessageAsync(user.Id, "Your request has been sent to the Admin.");
        }

        // Controller for UI interactions.
        private async Task HandleCallbackQueryAsync(ITelegramBotClient bot, CallbackQuery query)
        {
            if (query.Data == null) return;

            if (!_config.IsAdmin(query.From.Id)) return;

            long targetId = long.Parse(query.Data.Split('_')[1]);

            if (query.Data.StartsWith("approve_"))
            {
                _authService.AddUser(targetId);
                if (query.Message != null)
                    await bot.EditMessageTextAsync(query.Message.Chat.Id, query.Message.MessageId, $"Approved ID: {targetId}");

                await bot.SendTextMessageAsync(targetId, "Request approved! Send /help to start.");
            }
            await bot.AnswerCallbackQueryAsync(query.Id);
        }

        private Task HandleErrorAsync(ITelegramBotClient bot, Exception ex, CancellationToken ct)
        {
            Console.WriteLine($"[CRITICAL] {ex.Message}");
            return Task.CompletedTask;
        }
    }
}
