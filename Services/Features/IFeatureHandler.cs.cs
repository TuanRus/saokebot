using Telegram.Bot;
using SaoKeBot.Models;

namespace SaoKeBot.Services.Features
{
    // Interface defining common behavior for all features.
    public interface IFeatureHandler
    {
        // Check whether this feature can handle this transaction type.
        bool CanHandle(string type);

        // Execute the logic (save to DB, reply to the message...).
        Task HandleAsync(ITelegramBotClient bot, long chatId, long userId, ParseResult result);
    }
}
