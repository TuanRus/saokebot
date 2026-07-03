using Telegram.Bot;
using Telegram.Bot.Types.Enums;
using SaoKeBot.Models;
using SaoKeBot.Services.Repositories;

namespace SaoKeBot.Services.Features
{
    public class DebtFeature : IFeatureHandler
    {
        private readonly ITransactionRepository _repo;

        public DebtFeature(ITransactionRepository repo)
        {
            _repo = repo;
        }

        // Only handles the DEBT type (I owe others).
        public bool CanHandle(string type) => type == "DEBT";

        public async Task HandleAsync(ITelegramBotClient bot, long chatId, long userId, ParseResult result)
        {
            await _repo.AddAsync(userId, result);

            await bot.SendTextMessageAsync(chatId,
                $"📕 <b>I Owe: {result.Amount:N0}</b>\n (a liability to pay back)\n Note: {result.Note}",
                parseMode: ParseMode.Html);
        }
    }
}
